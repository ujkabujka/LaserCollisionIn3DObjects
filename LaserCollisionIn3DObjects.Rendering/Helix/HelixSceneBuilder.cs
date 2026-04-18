using System.Windows.Media;
using System.Windows.Media.Media3D;
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
        float defaultRayLength = 25f)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var visuals = new List<Visual3D>();
        var generatedRayLookup = scene.GeneratedRays.Count > 0 ? new HashSet<Ray3D>(scene.GeneratedRays) : null;
        visuals.AddRange(_frameVisualizer.CreateGlobalFrameVisuals(3f));

        visuals.Add(_meshFactory.CreateRectangularPrismBatch(scene.RectangularPrisms, Colors.LightGreen));
        visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(
            scene.RectangularPrisms.Select(prism => (prism.Frame, GetPrismFrameAxisLength(prism))).ToList()));

        visuals.Add(_meshFactory.CreateCylindricalLightSourceBatch(scene.CylindricalLightSources, Colors.Gold));
        visuals.AddRange(_frameVisualizer.CreateFrameVisualsBatch(
            scene.CylindricalLightSources.Select(lightSource => (lightSource.Frame, GetLightSourceFrameAxisLength(lightSource))).ToList()));

        var raySegments = new List<(Ray3D Ray, float Length)>(scene.Rays.Count);
        var generatedRayOriginsWithoutHit = new List<Ray3D>();

        List<RayHitResult> hitResultList = new List<RayHitResult>();
        foreach (var ray in scene.Rays)
        {
            RayHitResult? hit = null;
            var isGeneratedRay = generatedRayLookup?.Contains(ray) == true;
            var hasHit = hitResults is not null && hitResults.TryGetValue(ray, out hit) && hit is not null && hit.HasHit;

            if (isGeneratedRay && !hasHit)
            {
                generatedRayOriginsWithoutHit.Add(ray);
                continue;
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

        if(hitResultList.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateHitPoints(hitResultList, color: Colors.Red));
        }

        if (raySegments.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayLines(raySegments, color: Colors.OrangeRed));
        }

        if (generatedRayOriginsWithoutHit.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreateRayOriginPointBatch(generatedRayOriginsWithoutHit, color: Colors.OrangeRed));
        }

        if (scene.HolePoints.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreatePoints(scene.HolePoints, color: Colors.Blue));
        }

        return visuals;
    }

    public IReadOnlyList<Visual3D> BuildProjectionVisuals(
        IReadOnlyList<Point3> holePoints,
        ProjectionComputationResult? projectionResult)
    {
        ArgumentNullException.ThrowIfNull(holePoints);

        var visuals = new List<Visual3D>();
        if (holePoints.Count > 0)
        {
            visuals.Add(_rayVisualizer.CreatePoints(holePoints, Colors.DodgerBlue, size: 4));
        }

        if (projectionResult?.SourcePoint is { } sourcePoint)
        {
            visuals.Add(_rayVisualizer.CreatePoints(new[] { sourcePoint }, Colors.Gold, size: 7));
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

        return visuals;
    }

    private static float GetPrismFrameAxisLength(RectangularPrism prism)
    {
        return Math.Max(Math.Max(prism.SizeX, prism.SizeY), prism.SizeZ) * 0.65f;
    }

    private static float GetLightSourceFrameAxisLength(CylindricalLightSource source)
    {
        //return Math.Max(source.Height, source.Radius * 2f) * 0.65f;
        return 1f;
    }
}
