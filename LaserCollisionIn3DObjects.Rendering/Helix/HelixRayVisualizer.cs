using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
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
    public Visual3D CreateRayLines(IReadOnlyList<(DomainRay3D Ray, float Length)> rays, double thickness = 2d, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(rays);

        var points = new Point3DCollection(rays.Count * 2);
        foreach (var (ray, length) in rays)
        {
            points.Add(ToPoint3D(ray.Origin));
            points.Add(ToPoint3D(ray.GetPoint(length)));
        }

        return new LinesVisual3D
        {
            Color = color ?? Colors.OrangeRed,
            Thickness = thickness,
            Points = points,
        };
    }

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
    /// Creates a small sphere for a ray origin.
    /// </summary>
    public Visual3D CreateRayOriginPoint(DomainRay3D ray, double radius = 0.06d, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(ray);

        return new SphereVisual3D
        {
            Center = ToPoint3D(ray.Origin),
            Radius = radius,
            Fill = new SolidColorBrush(color ?? Colors.OrangeRed),
            ThetaDiv = 12,
            PhiDiv = 12,
        };
    }

    public Visual3D CreateRayOriginPointBatch(IReadOnlyList<DomainRay3D> rays, double radius = 0.06d, Color? color = null)
    {
        ArgumentNullException.ThrowIfNull(rays);

        if (rays.Count == 0)
        {
            return new ModelVisual3D();
        }

        //var sphereBuilder = new MeshBuilder();
        //sphereBuilder.AddSphere(new Point3D(0, 0, 0), radius, 10, 10);
        // var sphereMesh = sphereBuilder.ToMesh();

        //var material = MaterialHelper.CreateMaterial(color ?? Colors.OrangeRed);
        var points = new PointsVisual3D
        {
            Color = color ?? Colors.OrangeRed,
            Size = 3
        };
        //var group = new Model3DGroup();

        foreach (var ray in rays)
        {
            // group.Children.Add(new GeometryModel3D
            // {
            //     Geometry = sphereMesh,
            //     Material = material,
            //     BackMaterial = material,
            //     Transform = new TranslateTransform3D(ray.Origin.X, ray.Origin.Y, ray.Origin.Z),
            // });

            points.Points.Add(new Point3D(ray.Origin.X, ray.Origin.Y, ray.Origin.Z));
        }

        return  points;
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

    public Visual3D CreateHitPoints(IReadOnlyList<RayHitResult> hitResult, Color? color = null)
    {
         ArgumentNullException.ThrowIfNull(hitResult);

        if (hitResult.Count == 0)
        {
            return new ModelVisual3D();
        }

        var points = new PointsVisual3D
        {
            Color = color ?? Colors.Red,
            Size = 3
        };

        foreach (var hit in hitResult)
        {

            points.Points.Add(ToPoint3D(hit.HitPoint));
        }

        return  points;
    }

    public Visual3D CreateHitPoints(IReadOnlyList<Point3> holes, Color? color = null)
    {
         ArgumentNullException.ThrowIfNull(holes);

        if (holes.Count == 0)
        {
            return new ModelVisual3D();
        }

        var points = new PointsVisual3D
        {
            Color = color ?? Colors.Blue,
            Size = 3
        };

        foreach (var hole in holes)
        {

            points.Points.Add(new Point3D(hole.X, hole.Y, hole.Z));
        }

        return  points;
    }



    private static Point3D ToPoint3D(Vector3 value)
    {
        return new Point3D(value.X, value.Y, value.Z);
    }
}
