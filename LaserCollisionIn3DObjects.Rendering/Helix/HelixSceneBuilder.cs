using System.Windows.Media;
using System.Windows.Media.Media3D;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Rendering.Helix;

/// <summary>
/// Builds Helix visuals from domain scene data and optional hit results.
/// </summary>
public sealed class HelixSceneBuilder
{
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

        foreach (var prism in scene.RectangularPrisms)
        {
            visuals.Add(_meshFactory.CreateRectangularPrism(prism, Colors.SteelBlue));
        }

        foreach (var lightSource in scene.CylindricalLightSources)
        {
            visuals.Add(_meshFactory.CreateCylindricalLightSource(lightSource, Colors.Gold));
        }

        foreach (var ray in scene.Rays)
        {
            RayHitResult? hit = null;
            var hasHit = hitResults is not null && hitResults.TryGetValue(ray, out hit);
            var rayLength = hasHit && hit is not null && hit.HasHit ? hit.Distance : defaultRayLength;

            visuals.Add(_rayVisualizer.CreateRayLine(ray, rayLength, Colors.OrangeRed));

            if (hasHit && hit is not null)
            {
                var hitVisual = _rayVisualizer.CreateHitPoint(hit);
                if (hitVisual is not null)
                {
                    visuals.Add(hitVisual);
                }
            }
        }

        return visuals;
    }
}
