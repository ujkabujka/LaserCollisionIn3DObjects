using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Rendering.Helix;

/// <summary>
/// Builds Helix visuals from domain scene data and optional hit results.
/// </summary>
public sealed class HelixSceneBuilder
{
    private readonly HelixFrameVisualizer _frameVisualizer = new();
    private readonly HelixMeshFactory _meshFactory = new();
    private readonly HelixRayVisualizer _rayVisualizer = new();

    /// <summary>
    /// Builds visuals for prisms, rays, and hit points.
    /// </summary>
    /// <param name="scene">Domain scene data.</param>
    /// <param name="hitResults">Optional ray-hit mapping keyed by the same ray instances found in <paramref name="scene"/>.</param>
    /// <param name="defaultRayLength">Length used when no hit exists for a ray.</param>
    /// <returns>Visual collection ready for insertion into <c>HelixViewport3D.Children</c>.</returns>
    public IReadOnlyList<Visual3D> BuildVisuals(
        SceneModel scene,
        IReadOnlyDictionary<Ray3D, RayHitResult>? hitResults = null,
        float defaultRayLength = 25f,
        CollisionSceneVisualOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= new CollisionSceneVisualOptions();

        var visuals = new List<Visual3D>();
        var generatedRayLookup = scene.GeneratedRays.Count > 0 ? new HashSet<Ray3D>(scene.GeneratedRays) : null;
        var projectedRayLookup = scene.ProjectedSourceRays.Count > 0 ? new HashSet<Ray3D>(scene.ProjectedSourceRays) : null;
        visuals.AddRange(_frameVisualizer.CreateGlobalFrameVisuals(3f));

        visuals.Add(_meshFactory.CreateRectangularPrismBatch(scene.RectangularPrisms, Colors.LightGreen));
        visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(
            scene.RectangularPrisms.Select(prism => (prism.Frame, GetPrismFrameAxisLength(prism))).ToList()));

        visuals.Add(_meshFactory.CreateCylindricalLightSourceBatch(scene.CylindricalLightSources, Colors.Gold));
        visuals.Add(_meshFactory.CreateAxisymmetricLightSourceBatch(scene.AxisymmetricLightSources, Colors.Goldenrod));

        var lightSourceFrames = scene.CylindricalLightSources
            .Select(lightSource => (lightSource.Frame, GetLightSourceFrameAxisLength(lightSource)))
            .Concat(scene.AxisymmetricLightSources.Select(lightSource => (lightSource.Frame, GetLightSourceFrameAxisLength(lightSource))))
            .ToList();
        visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(lightSourceFrames));

        var raySegments = new List<(Ray3D Ray, float Length)>(scene.Rays.Count);
        var generatedRayOriginsWithoutHit = new List<Ray3D>();
        var projectedRayOrigins = new List<Ray3D>();

        List<RayHitResult> hitResultList = new List<RayHitResult>();
        foreach (var ray in scene.Rays)
        {
            RayHitResult? hit = null;
            var isGeneratedRay = generatedRayLookup?.Contains(ray) == true;
            var isProjectedSourceRay = projectedRayLookup?.Contains(ray) == true;
            var hasHit = hitResults is not null && hitResults.TryGetValue(ray, out hit) && hit is not null && hit.HasHit;

            if (isGeneratedRay && !hasHit)
            {
                generatedRayOriginsWithoutHit.Add(ray);
                continue;
            }

            // Projected source rays are collision candidates.
            // Their origins should always be visible as source samples,
            // but their lines are rendered only after a collision hit exists.
            if (isProjectedSourceRay)
            {
                projectedRayOrigins.Add(ray);

                if (!hasHit)
                {
                    continue;
                }
            }

            var rayLength = hasHit && hit is not null ? hit.Distance : defaultRayLength;
            raySegments.Add((ray, rayLength));

            if (hasHit && hit is not null)
            {
                //var hitVisual = _rayVisualizer.CreateHitPoint(hit);
                // if (hitVisual is not null)
                // {
                //     visuals.Add(hitVisual);
                // }
                hitResultList.Add(hit);
            }
        }

        if(options.ShowCollisionHitPoints && hitResultList.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateHitPoints(hitResultList, color: Colors.Red));
        }

        if (options.ShowCollisionRays && raySegments.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayLines(raySegments, color: Colors.OrangeRed));
        }

        if (generatedRayOriginsWithoutHit.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayOriginPointBatch(generatedRayOriginsWithoutHit, color: Colors.OrangeRed));
        }

        if (projectedRayOrigins.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayOriginPointBatch(projectedRayOrigins, radius: 0.08d, color: Colors.DeepPink));
        }

        if (scene.HolePoints.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreatePoints(scene.HolePoints, color: Colors.DodgerBlue));
        }
        if (scene.NaturalPoints.Count > 0)
            visuals.Add(_rayVisualizer.CreatePoints(scene.NaturalPoints, color: Colors.Orange));

        return visuals;
    }


    public IReadOnlyList<Visual3D> BuildSourceCompletionPreviewVisuals(
        PointSourceFrameState? sourceFrame,
        IAxisymmetricSourceProfile? profile,
        IReadOnlyList<Ray3D>? originalRays,
        IReadOnlyList<Ray3D>? syntheticRays)
    {
        var visuals = new List<Visual3D>();

        if (sourceFrame is null || profile is null)
        {
            return visuals;
        }

        var frame = ToFrame3D(sourceFrame);
        visuals.Add(_meshFactory.CreateAxisymmetricSourceProfileVisual(profile, frame, Colors.Goldenrod, 0.45d, slices: 32, stacks: 24));
        visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(new[] { (frame, 0.9f) }));

        if (originalRays is not null && originalRays.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayOriginPointBatch(originalRays, color: Colors.OrangeRed));
        }

        if (syntheticRays is not null && syntheticRays.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayOriginPointBatch(syntheticRays, color: Colors.LimeGreen));
        }

        return visuals;
    }

    public IReadOnlyList<Visual3D> BuildProjectionVisuals(
        IReadOnlyList<Point3> holePoints,
        IReadOnlyList<Point3> naturalPoints,
        ProjectionComputationResult? projectionResult,
        IAxisymmetricSourceProfile? previewProfile = null,
        Frame3D? previewFrame = null,
        bool previewAsGhost = true,
        Point3? previewTiltPointLocal = null,
        IReadOnlyList<RectangularPrism>? panels = null,
        IReadOnlyList<Point3>? measuredCornerPoints = null)
    {
        ArgumentNullException.ThrowIfNull(holePoints);
        ArgumentNullException.ThrowIfNull(naturalPoints);

        var visuals = new List<Visual3D>();
        if (panels is { Count: > 0 })
        {
            visuals.Add(_meshFactory.CreateRectangularPrismBatch(panels, Colors.LightGreen, opacity: 0.5d));
        }

        if (measuredCornerPoints is { Count: > 0 })
        {
            visuals.Add(_rayVisualizer.CreatePoints(measuredCornerPoints, Colors.Red, size: 7));
        }

        if (holePoints.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreatePoints(holePoints, Colors.DodgerBlue, size: 4));
        }
        if (naturalPoints.Count > 0)
            visuals.Add(_rayVisualizer.CreatePoints(naturalPoints, Colors.Orange, size: 4));

        if (projectionResult?.SourceFrame is { } sourceFrame)
        {
            visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(new[] { (ToFrame3D(sourceFrame), 1.5f) }));
        }

        if (previewProfile is not null && previewFrame is not null)
        {
            var previewOpacity = previewAsGhost ? 0.25d : 1d;
            visuals.Add(_meshFactory.CreateAxisymmetricSourceProfileVisual(previewProfile, previewFrame, Colors.MediumPurple, previewOpacity, slices: 32, stacks: 24));
            visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(new[] { (previewFrame, 1.5f) }));

            if (previewTiltPointLocal is Point3 tiltLocal)
            {
                var tiltWorld = previewFrame.TransformPointToWorld(new Vector3((float)tiltLocal.X, (float)tiltLocal.Y, (float)tiltLocal.Z));
                visuals.Add(_rayVisualizer.CreatePoints(new[] { new Point3(tiltWorld.X, tiltWorld.Y, tiltWorld.Z) }, Colors.Orange, size: 8));
            }
        }

        if (projectionResult?.AxisymmetricSource is { } axisymmetric)
        {
            if (axisymmetric.Points.Count > 0)
            {
                visuals.Add(_rayVisualizer.CreatePoints(axisymmetric.Points.Select(point => point.SourceSurfacePoint).ToList(), Colors.Red, size: 5));
            }
        }
        else
        {
            if (projectionResult?.PointSourceOrigin is { } pointSourceOrigin)
            {
                visuals.Add(_rayVisualizer.CreatePoints(new[] { pointSourceOrigin }, Colors.OrangeRed, size: 5));
            }

            if (projectionResult is not null && projectionResult.Rays.Count > 0)
            {
                var segments = projectionResult.Rays
                    .Select(projectionRay =>
                    {
                        var dx = projectionRay.TargetHolePoint.X - projectionRay.Ray.Origin.X;
                        var dy = projectionRay.TargetHolePoint.Y - projectionRay.Ray.Origin.Y;
                        var dz = projectionRay.TargetHolePoint.Z - projectionRay.Ray.Origin.Z;
                        var length = (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                        return (projectionRay.Ray, length);
                    })
                    .ToList();

                visuals.Add(_rayVisualizer.CreateRayLines(segments, color: Colors.Orange));
            }
        }

        return visuals;
    }

    private static float GetPrismFrameAxisLength(RectangularPrism prism)
    {
        return Math.Max(Math.Max(prism.SizeX, prism.SizeY), prism.SizeZ) * 0.65f;
    }

    private static float GetLightSourceFrameAxisLength(CylindricalLightSource source)
    {
        return Math.Max(source.Height, source.Radius * 2f) * 0.25f;
    }

    private static float GetLightSourceFrameAxisLength(AxisymmetricLightSource source)
    {
        var startRadius = source.Profile.RadiusAt(0f);
        var endRadius = source.Profile.RadiusAt(source.Profile.Length);
        return Math.Max(source.Profile.Length, Math.Max(startRadius, endRadius) * 2f) * 0.25f;
    }

    private static Frame3D ToFrame3D(PointSourceFrameState frame)
    {
        var x = new Vector3((float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z);
        var y = new Vector3((float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z);
        var z = new Vector3((float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z);
        var matrix = new Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            0, 0, 0, 1);
        var orientation = System.Numerics.Quaternion.CreateFromRotationMatrix(matrix);
        return new Frame3D(
            new Vector3((float)frame.Origin.X, (float)frame.Origin.Y, (float)frame.Origin.Z),
            orientation);
    }
}
