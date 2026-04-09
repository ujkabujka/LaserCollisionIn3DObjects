using System.Numerics;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Scene;
using LaserCollisionIn3DObjects.Rendering.Helix;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using DomainRay3D = LaserCollisionIn3DObjects.Domain.Geometry.Ray3D;

namespace LaserCollisionIn3DObjects.Wpf.Services;

/// <summary>
/// Coordinates UI scene data, domain collision calculations, rendering visuals, and viewport updates.
/// </summary>
public sealed class SceneRenderSyncService
{
    public sealed record SceneSyncResult(
        IReadOnlyList<HitResultItemViewModel> HitRows,
        TimeSpan CollisionDuration,
        CollisionAlgorithmOption? CollisionAlgorithm);

    private readonly HelixViewport3D _viewport;
    private readonly ModelVisual3D _dynamicVisualRoot = new();
    private readonly HelixSceneBuilder _sceneBuilder = new();
    private readonly CylindricalRayGenerator _rayGenerator = new();

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
        bool runCollision,
        CollisionAlgorithmOption algorithm)
    {
        var scene = BuildDomainScene(prismItems, lightSourceItems, rayItems);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var collisionResults = runCollision ? CalculateFirstHits(scene, algorithm) : new List<(DomainRay3D Ray, RayHitResult Hit)>();
        stopwatch.Stop();

        var hitLookup = collisionResults
            .Where(result => result.Hit.HasHit)
            .ToDictionary(result => result.Ray, result => result.Hit);

        var visuals = _sceneBuilder.BuildVisuals(scene, hitLookup);
        UpdateViewport(visuals);

        return new SceneSyncResult(
            BuildHitRows(scene, collisionResults),
            runCollision ? stopwatch.Elapsed : TimeSpan.Zero,
            runCollision ? algorithm : null);
    }

    private SceneModel BuildDomainScene(
        IReadOnlyList<PrismItemViewModel> prisms,
        IReadOnlyList<CylindricalLightSourceItemViewModel> lightSources,
        IReadOnlyList<RayItemViewModel> rays)
    {
        var scene = new SceneModel();

        foreach (var prism in prisms)
        {
            var orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(
                prism.BaseOrientation,
                prism.RotationX,
                prism.RotationY,
                prism.RotationZ);

            scene.RectangularPrisms.Add(
                new RectangularPrism(
                    string.IsNullOrWhiteSpace(prism.Name) ? "Prism" : prism.Name,
                    new Frame3D(new Vector3(prism.PositionX, prism.PositionY, prism.PositionZ), orientation),
                    prism.SizeX,
                    prism.SizeY,
                    prism.SizeZ));
        }

        foreach (var lightSource in lightSources)
        {
            var orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(
                lightSource.BaseOrientation,
                lightSource.RotationX,
                lightSource.RotationY,
                lightSource.RotationZ);

            var domainSource = new CylindricalLightSource(
                string.IsNullOrWhiteSpace(lightSource.Name) ? "Light Source" : lightSource.Name,
                new Frame3D(new Vector3(lightSource.PositionX, lightSource.PositionY, lightSource.PositionZ), orientation),
                lightSource.Radius,
                lightSource.Height,
                lightSource.RayCount);

            scene.CylindricalLightSources.Add(domainSource);
            var generatedRays = _rayGenerator.Generate(domainSource);
            scene.GeneratedRays.AddRange(generatedRays);
            scene.Rays.AddRange(generatedRays);
        }

        foreach (var ray in rays)
        {
            scene.Rays.Add(
                new DomainRay3D(
                    new Vector3(ray.OriginX, ray.OriginY, ray.OriginZ),
                    new Vector3(ray.DirectionX, ray.DirectionY, ray.DirectionZ)));
        }

        return scene;
    }

    private static List<(DomainRay3D Ray, RayHitResult Hit)> CalculateFirstHits(SceneModel scene, CollisionAlgorithmOption algorithm)
    {
        return algorithm switch
        {
            CollisionAlgorithmOption.ClosestHitParallel => CalculateFirstHitsParallel(scene),
            _ => CalculateFirstHitsSequential(scene),
        };
    }

    private static List<(DomainRay3D Ray, RayHitResult Hit)> CalculateFirstHitsSequential(SceneModel scene)
    {
        var results = new List<(DomainRay3D Ray, RayHitResult Hit)>(scene.Rays.Count);

        foreach (var ray in scene.Rays)
        {
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
        }

        return results;
    }

    private static List<(DomainRay3D Ray, RayHitResult Hit)> CalculateFirstHitsParallel(SceneModel scene)
    {
        var results = new (DomainRay3D Ray, RayHitResult Hit)[scene.Rays.Count];

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
        });

        return results.ToList();
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

    private void UpdateViewport(IReadOnlyList<Visual3D> visuals)
    {
        _dynamicVisualRoot.Children.Clear();
        foreach (var visual in visuals)
        {
            _dynamicVisualRoot.Children.Add(visual);
        }
    }
}
