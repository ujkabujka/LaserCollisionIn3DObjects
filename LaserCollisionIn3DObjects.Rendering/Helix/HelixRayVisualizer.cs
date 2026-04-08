using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Geometry;
using DomainRay3D = LaserCollisionIn3DObjects.Domain.Geometry.Ray3D;

namespace LaserCollisionIn3DObjects.Rendering.Helix;

/// <summary>
/// Creates line and point visuals for rays and ray hit results.
/// </summary>
public sealed class HelixRayVisualizer
{
    /// <summary>
    /// Creates a line visual that represents a ray.
    /// </summary>
    /// <param name="ray">Domain ray.</param>
    /// <param name="length">Rendered length in scene units.</param>
    /// <param name="color">Optional line color.</param>
    /// <returns>A line visual suitable for <see cref="HelixViewport3D"/>.</returns>
    public Visual3D CreateRayLine(DomainRay3D ray, float length, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(ray);

        var start = ToPoint3D(ray.Origin);
        var end = ToPoint3D(ray.GetPoint(length));

        return new LinesVisual3D
        {
            Color = color ?? Colors.OrangeRed,
            Thickness = 2,
            Points = new Point3DCollection { start, end },
        };
    }

    /// <summary>
    /// Creates a small sphere for a ray hit point.
    /// </summary>
    /// <param name="hitResult">Domain hit result.</param>
    /// <param name="radius">Sphere radius.</param>
    /// <param name="color">Optional sphere color.</param>
    /// <returns>A sphere visual when a hit exists; otherwise <see langword="null"/>.</returns>
    public Visual3D? CreateHitPoint(RayHitResult hitResult, double radius = 0.08d, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(hitResult);

        if (!hitResult.HasHit)
        {
            return null;
        }

        return new SphereVisual3D
        {
            Center = ToPoint3D(hitResult.HitPoint),
            Radius = radius,
            Fill = new SolidColorBrush(color ?? Colors.LimeGreen),
            ThetaDiv = 18,
            PhiDiv = 18,
        };
    }

    private static Point3D ToPoint3D(Vector3 value)
    {
        return new Point3D(value.X, value.Y, value.Z);
    }
}
