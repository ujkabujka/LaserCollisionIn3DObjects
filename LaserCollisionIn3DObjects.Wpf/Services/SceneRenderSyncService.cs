using System.Numerics;
using System.Windows.Media.Media3D;
using NumericsQuaternion = System.Numerics.Quaternion;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Scene;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Rendering.Helix;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using DomainRay3D = LaserCollisionIn3DObjects.Domain.Geometry.Ray3D;
using System.Diagnostics;

namespace LaserCollisionIn3DObjects.Wpf.Services;

/// <summary>
/// Coordinates UI scene data, domain collision calculations, rendering visuals, and viewport updates.
/// </summary>
public sealed class SceneRenderSyncService
{
    public sealed record CollisionComputation(
        SceneModel Scene,
        IReadOnlyList<(DomainRay3D Ray, RayHitResult Hit)> Hits,
        string SceneName,
        TimeSpan Duration,
        CollisionAlgorithmOption Algorithm,
        PanelCollisionAnalysis PanelAnalysis);
    public sealed record SceneSyncResult(
        IReadOnlyList<HitResultItemViewModel> HitRows,
        IReadOnlyList<CollisionHitPointRecord> HitPointRecords,
        PanelCollisionAnalysis? PanelAnalysis,
        TimeSpan CollisionDuration,
        CollisionAlgorithmOption? CollisionAlgorithm);

    private sealed record SceneBuildResult(SceneModel Scene);

    private readonly HelixViewport3D _viewport;
    private readonly ModelVisual3D _dynamicVisualRoot = new();
    private readonly HelixSceneBuilder _sceneBuilder = new();
    private readonly CylindricalRayGenerator _cylindricalRayGenerator = new();
    private readonly AxisymmetricRayGenerator _axisymmetricRayGenerator = new();

    public SceneRenderSyncService(HelixViewport3D viewport)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _viewport.Children.Add(_dynamicVisualRoot);
    }

    /// <summary>
    /// Synchronizes current editable scene data to the viewport and optionally computes hit results.
    /// </summary>
    public SceneSyncResult SyncScene(
        IReadOnlyList<PrismItemViewModel> prismItems,
        IReadOnlyList<CylindricalLightSourceItemViewModel> lightSourceItems,
        IReadOnlyList<RayItemViewModel> rayItems,
        IReadOnlyList<ProjectedLightSourceItemViewModel> projectedLightSources,
        IReadOnlyList<Point3> holePoints,
        IReadOnlyList<Point3> naturalPoints,
        string sceneName,
        bool runCollision,
        CollisionAlgorithmOption algorithm)
    {
        var renderStopwatch = Stopwatch.StartNew();
        Trace.WriteLine($"[CollisionRender] SyncScene started: prisms={prismItems.Count}, generatedSources={lightSourceItems.Count}, transferredSources={projectedLightSources.Count}, manualRays={rayItems.Count}, runCollision={runCollision}.");
        var buildResult = BuildDomainScene(prismItems, lightSourceItems, rayItems, projectedLightSources, holePoints, naturalPoints);
        Trace.WriteLine($"[CollisionRender] BuildDomainScene completed in {renderStopwatch.ElapsedMilliseconds} ms.");
        var scene = buildResult.Scene;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var collisionResults = runCollision ? CalculateFirstHits(scene, algorithm) : new List<(DomainRay3D Ray, RayHitResult Hit)>();
        stopwatch.Stop();

        var hitLookup = collisionResults
            .Where(result => result.Hit.HasHit)
            .ToDictionary(result => result.Ray, result => result.Hit);

        var visuals = _sceneBuilder.BuildVisuals(scene, hitLookup);
        Trace.WriteLine($"[CollisionRender] BuildVisuals completed in {renderStopwatch.ElapsedMilliseconds} ms; visuals={visuals.Count}.");
        UpdateViewport(visuals);
        Trace.WriteLine($"[CollisionRender] UpdateViewport completed in {renderStopwatch.ElapsedMilliseconds} ms.");

        return new SceneSyncResult(
            BuildHitRows(scene, collisionResults),
            runCollision ? BuildHitPointRecords(sceneName, collisionResults, scene.CollisionRayInputs) : Array.Empty<CollisionHitPointRecord>(),
            runCollision ? BuildPanelAnalysis(sceneName, scene, collisionResults) : null,
            runCollision ? stopwatch.Elapsed : TimeSpan.Zero,
            runCollision ? algorithm : null);
    }

    /// <summary>Performs domain-only scene generation and intersections; no WPF visuals are touched.</summary>
    public CollisionComputation ComputeCollision(
        IReadOnlyList<PrismItemViewModel> prismItems,
        IReadOnlyList<CylindricalLightSourceItemViewModel> lightSourceItems,
        IReadOnlyList<RayItemViewModel> rayItems,
        IReadOnlyList<ProjectedLightSourceItemViewModel> projectedLightSources,
        IReadOnlyList<Point3> holePoints,
        IReadOnlyList<Point3> naturalPoints,
        string sceneName,
        CollisionAlgorithmOption algorithm,
        IProgress<(int Processed, int Total)>? progress = null)
    {
        var scene = BuildDomainScene(prismItems, lightSourceItems, rayItems, projectedLightSources, holePoints, naturalPoints).Scene;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var hits = CalculateFirstHits(scene, algorithm, progress);
        stopwatch.Stop();
        return new CollisionComputation(scene, hits, sceneName, stopwatch.Elapsed, algorithm, BuildPanelAnalysis(sceneName, scene, hits));
    }

    /// <summary>Publishes a completed computation to Helix on the UI thread.</summary>
    public SceneSyncResult RenderCollision(CollisionComputation computation)
    {
        var hitLookup = computation.Hits.Where(x => x.Hit.HasHit).ToDictionary(x => x.Ray, x => x.Hit);
        UpdateViewport(_sceneBuilder.BuildVisuals(computation.Scene, hitLookup));
        return new SceneSyncResult(
            BuildHitRows(computation.Scene, computation.Hits),
            BuildHitPointRecords(computation.SceneName, computation.Hits, computation.Scene.CollisionRayInputs),
            computation.PanelAnalysis,
            computation.Duration,
            computation.Algorithm);
    }

    private SceneBuildResult BuildDomainScene(
        IReadOnlyList<PrismItemViewModel> prisms,
        IReadOnlyList<CylindricalLightSourceItemViewModel> lightSources,
        IReadOnlyList<RayItemViewModel> rays,
        IReadOnlyList<ProjectedLightSourceItemViewModel> projectedLightSources,
        IReadOnlyList<Point3> holePoints,
        IReadOnlyList<Point3> naturalPoints)
    {
        var scene = new SceneModel();

        foreach (var prism in prisms)
        {
            scene.RectangularPrisms.Add(PrismGeometryConverter.CreateDomainPrism(prism));
        }

        foreach (var lightSource in lightSources)
        {
            var orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(
                lightSource.BaseOrientation,
                lightSource.RotationX,
                lightSource.RotationY,
                lightSource.RotationZ);

            var frame = new Frame3D(new Vector3(lightSource.PositionX, lightSource.PositionY, lightSource.PositionZ), orientation);
            var tiltPoint = new Vector3(lightSource.TiltPointX, lightSource.TiltPointY, lightSource.TiltPointZ);

            List<DomainRay3D> generatedRays;
            CollisionRaySourceType sourceType;

            if (lightSource.SourceKind == AxisymmetricSourceKind.Cylinder)
            {
                var cylindricalSource = new CylindricalLightSource(
                    string.IsNullOrWhiteSpace(lightSource.Name) ? "Light Source" : lightSource.Name,
                    frame,
                    lightSource.Radius,
                    lightSource.Height,
                    lightSource.RayCount,
                    lightSource.TiltWeight,
                    tiltPoint);

                scene.CylindricalLightSources.Add(cylindricalSource);
                generatedRays = _cylindricalRayGenerator.Generate(cylindricalSource);
                sourceType = CollisionRaySourceType.CylindricalGenerated;
            }
            else
            {
                var profile = BuildAxisymmetricProfile(lightSource);

                var axisymmetricSource = new AxisymmetricLightSource(
                    string.IsNullOrWhiteSpace(lightSource.Name) ? "Light Source" : lightSource.Name,
                    frame,
                    lightSource.SourceKind,
                    profile,
                    lightSource.RayCount,
                    lightSource.TiltWeight,
                    tiltPoint);

                scene.AxisymmetricLightSources.Add(axisymmetricSource);
                generatedRays = _axisymmetricRayGenerator.Generate(axisymmetricSource);
                sourceType = lightSource.SourceKind switch
                {
                    AxisymmetricSourceKind.ConicalFrustum => CollisionRaySourceType.ConicalFrustumGenerated,
                    AxisymmetricSourceKind.CircularOgive => CollisionRaySourceType.CircularOgiveGenerated,
                    AxisymmetricSourceKind.Hybrid => CollisionRaySourceType.HybridAxisymmetricGenerated,
                    _ => throw new ArgumentOutOfRangeException(nameof(lightSource.SourceKind), "Unsupported axisymmetric source kind."),
                };
            }

            scene.GeneratedRays.AddRange(generatedRays);
            scene.Rays.AddRange(generatedRays);
            scene.CollisionRayInputs.AddRange(generatedRays.Select(ray => new SceneModel.CollisionRayInput(ray, sourceType, lightSource.Name)));
        }


        foreach (var projectedSource in projectedLightSources)
        {
            var frame = BuildFrame(projectedSource.SourceFrame, projectedSource.BaseOrientation);
            var profile = projectedSource.ProfileDefinition.BuildProfile();
            var axisymmetricSource = new AxisymmetricLightSource(
                string.IsNullOrWhiteSpace(projectedSource.Name) ? "Projected Source" : projectedSource.Name,
                frame,
                projectedSource.ProfileDefinition.Kind,
                profile,
                projectedSource.EffectiveRayCount,
                0f,
                Vector3.Zero);

            scene.AxisymmetricLightSources.Add(axisymmetricSource);

            var exactRays = projectedSource.GetEffectiveCollisionRays();
            var sourceType = projectedSource.OriginKind switch
            {
                ProjectedLightSourceOriginKind.CompletedProjectionResult => CollisionRaySourceType.CompletedProjectionResult,
                ProjectedLightSourceOriginKind.ImportedTextFile => CollisionRaySourceType.ImportedLightSource,
                _ => CollisionRaySourceType.ProjectionResult,
            };
            foreach (var exactRay in exactRays)
            {
                scene.ProjectedSourceRays.Add(exactRay);
                scene.Rays.Add(exactRay);
                scene.CollisionRayInputs.Add(new SceneModel.CollisionRayInput(exactRay, sourceType, projectedSource.Name));
            }
        }

        foreach (var ray in rays)
        {
            var domainRay = new DomainRay3D(
                    new Vector3(ray.OriginX, ray.OriginY, ray.OriginZ),
                    new Vector3(ray.DirectionX, ray.DirectionY, ray.DirectionZ));
            scene.Rays.Add(domainRay);
            scene.CollisionRayInputs.Add(new SceneModel.CollisionRayInput(domainRay, CollisionRaySourceType.Manual, "Manual Ray"));
        }

        foreach (var hole in holePoints)
        {
            scene.HolePoints.Add(hole);
        }
        scene.NaturalPoints.AddRange(naturalPoints);

        return new SceneBuildResult(scene);
    }

    private static List<(DomainRay3D Ray, RayHitResult Hit)> CalculateFirstHits(SceneModel scene, CollisionAlgorithmOption algorithm, IProgress<(int Processed, int Total)>? progress = null)
    {
        return algorithm switch
        {
            CollisionAlgorithmOption.ClosestHitParallel => CalculateFirstHitsParallel(scene, progress),
            _ => CalculateFirstHitsSequential(scene, progress),
        };
    }

    private static List<(DomainRay3D Ray, RayHitResult Hit)> CalculateFirstHitsSequential(SceneModel scene, IProgress<(int Processed, int Total)>? progress = null)
    {
        var results = new List<(DomainRay3D Ray, RayHitResult Hit)>(scene.Rays.Count);

        for (var rayIndex = 0; rayIndex < scene.Rays.Count; rayIndex++)
        {
            var ray = scene.Rays[rayIndex];
            var closestHit = RayHitResult.NoHit;

            foreach (var prism in scene.RectangularPrisms)
            {
                var hit = prism.Intersect(ray);
                if (hit.HasHit && hit.Distance < closestHit.Distance)
                {
                    closestHit = hit;
                }
            }

            results.Add((ray, closestHit));
            ReportProgress(progress, rayIndex + 1, scene.Rays.Count);
        }

        return results;
    }



    private static Frame3D BuildFrame(PointSourceFrameState sourceFrame, NumericsQuaternion fallbackOrientation)
    {
        var x = new Vector3((float)sourceFrame.AxisX.X, (float)sourceFrame.AxisX.Y, (float)sourceFrame.AxisX.Z);
        var y = new Vector3((float)sourceFrame.AxisY.X, (float)sourceFrame.AxisY.Y, (float)sourceFrame.AxisY.Z);
        var z = new Vector3((float)sourceFrame.AxisZ.X, (float)sourceFrame.AxisZ.Y, (float)sourceFrame.AxisZ.Z);
        var matrix = new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            0, 0, 0, 1);
        var orientation = NumericsQuaternion.CreateFromRotationMatrix(matrix);
        if (!float.IsFinite(orientation.X) || !float.IsFinite(orientation.Y) || !float.IsFinite(orientation.Z) || !float.IsFinite(orientation.W))
        {
            orientation = fallbackOrientation;
        }

        return new Frame3D(new Vector3((float)sourceFrame.Origin.X, (float)sourceFrame.Origin.Y, (float)sourceFrame.Origin.Z), orientation);
    }

    private static IAxisymmetricSourceProfile BuildAxisymmetricProfile(CylindricalLightSourceItemViewModel lightSource)
    {
        return lightSource.SourceKind switch
        {
            AxisymmetricSourceKind.ConicalFrustum => new ConicalFrustumSourceProfile(lightSource.RadiusStart, lightSource.RadiusEnd, lightSource.Length),
            AxisymmetricSourceKind.CircularOgive => new CircularOgiveSourceProfile(lightSource.RadiusStart, lightSource.RadiusEnd, lightSource.Length, lightSource.ArcRadius, lightSource.OgiveCurvatureDirection),
            AxisymmetricSourceKind.Hybrid => new HybridAxisymmetricSourceProfile(lightSource.HybridSegments.Select(segment =>
                new HybridAxisymmetricSourceSegmentDefinition(
                    segment.SegmentKind,
                    segment.Length,
                    segment.RadiusStart,
                    segment.RadiusEnd,
                    segment.SegmentKind == HybridAxisymmetricSourceSegmentKind.CircularOgive ? segment.ArcRadius : null,
                    segment.OgiveCurvatureDirection)).ToList()),
            _ => throw new ArgumentOutOfRangeException(nameof(lightSource.SourceKind), "Unsupported axisymmetric source kind."),
        };
    }
    private static List<(DomainRay3D Ray, RayHitResult Hit)> CalculateFirstHitsParallel(SceneModel scene, IProgress<(int Processed, int Total)>? progress = null)
    {
        var results = new (DomainRay3D Ray, RayHitResult Hit)[scene.Rays.Count];

        var processed = 0;
        Parallel.For(0, scene.Rays.Count, i =>
        {
            var ray = scene.Rays[i];
            var closestHit = RayHitResult.NoHit;

            foreach (var prism in scene.RectangularPrisms)
            {
                var hit = prism.Intersect(ray);
                if (hit.HasHit && hit.Distance < closestHit.Distance)
                {
                    closestHit = hit;
                }
            }

            results[i] = (ray, closestHit);
            ReportProgress(progress, Interlocked.Increment(ref processed), scene.Rays.Count);
        });

        return results.ToList();
    }

    private static void ReportProgress(IProgress<(int Processed, int Total)>? progress, int processed, int total)
    {
        if (progress is null || total == 0) return;
        var bucket = Math.Max(1, total / 100);
        if (processed == total || processed % bucket == 0) progress.Report((processed, total));
    }

    private static IReadOnlyList<HitResultItemViewModel> BuildHitRows(
        SceneModel scene,
        IReadOnlyList<(DomainRay3D Ray, RayHitResult Hit)> hitResults)
    {
        var rows = new List<HitResultItemViewModel>();

        for (var i = 0; i < scene.Rays.Count; i++)
        {
            var row = new HitResultItemViewModel
            {
                RayLabel = $"Ray {i + 1}",
                HasHit = false,
                Distance = 0f,
                PrismName = "-",
                SourceType = i < scene.CollisionRayInputs.Count ? scene.CollisionRayInputs[i].SourceType.ToString() : string.Empty,
                SourceName = i < scene.CollisionRayInputs.Count ? scene.CollisionRayInputs[i].SourceName : string.Empty,
            };

            if (i < hitResults.Count)
            {
                var hit = hitResults[i].Hit;
                row.HasHit = hit.HasHit;
                row.Distance = hit.HasHit ? hit.Distance : 0f;
                row.PrismName = hit.HitObject?.Name ?? "-";
            }

            rows.Add(row);
        }

        return rows;
    }

    private static IReadOnlyList<CollisionHitPointRecord> BuildHitPointRecords(
        string sceneName,
        IReadOnlyList<(DomainRay3D Ray, RayHitResult Hit)> hitResults,
        IReadOnlyList<SceneModel.CollisionRayInput> rayInputs)
    {
        var records = new List<CollisionHitPointRecord>();

        for (var i = 0; i < hitResults.Count && i < rayInputs.Count; i++)
        {
            var hit = hitResults[i].Hit;
            if (!hit.HasHit)
            {
                continue;
            }

            records.Add(new CollisionHitPointRecord(sceneName, hit.HitPoint, rayInputs[i].SourceType, rayInputs[i].SourceName));
        }

        return records;
    }

    private static PanelCollisionAnalysis BuildPanelAnalysis(
        string sceneName, SceneModel scene, IReadOnlyList<(DomainRay3D Ray, RayHitResult Hit)> hitResults)
    {
        var inputs = new List<PanelCollisionInputHit>();
        for (var i = 0; i < hitResults.Count; i++)
        {
            var provenance = i < scene.CollisionRayInputs.Count
                ? scene.CollisionRayInputs[i]
                : new SceneModel.CollisionRayInput(hitResults[i].Ray, CollisionRaySourceType.Manual, string.Empty);
            inputs.Add(new PanelCollisionInputHit(i, hitResults[i].Hit, provenance.SourceType, provenance.SourceName));
        }
        return new PanelCollisionAnalysisService().Build(sceneName, scene.RectangularPrisms, inputs, scene.Rays.Count);
    }

    private void UpdateViewport(IReadOnlyList<Visual3D> visuals)
    {
        _dynamicVisualRoot.Children.Clear();
        foreach (var visual in visuals)
        {
            _dynamicVisualRoot.Children.Add(visual);
        }
    }
}
