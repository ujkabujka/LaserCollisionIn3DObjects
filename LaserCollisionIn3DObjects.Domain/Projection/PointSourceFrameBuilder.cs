using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public static class PointSourceFrameBuilder
{
    private const double ParallelTolerance = 1e-6;

    public static PointSourceFrameState Build(Point3 origin, Vector3D xDirection, Vector3D yDirection)
    {
        var x = ToVector3(xDirection);
        var y = ToVector3(yDirection);

        if (x.LengthSquared() <= 0f)
        {
            throw new ArgumentException("Source frame X direction must be non-zero.", nameof(xDirection));
        }

        if (y.LengthSquared() <= 0f)
        {
            throw new ArgumentException("Source frame Y direction must be non-zero.", nameof(yDirection));
        }

        var axisX = Vector3.Normalize(x);
        var yOrthogonal = y - (Vector3.Dot(y, axisX) * axisX);
        if (yOrthogonal.LengthSquared() <= ParallelTolerance)
        {
            throw new ArgumentException("Source frame Y direction is parallel (or nearly parallel) to X.", nameof(yDirection));
        }

        var axisY = Vector3.Normalize(yOrthogonal);
        var axisZ = Vector3.Normalize(Vector3.Cross(axisX, axisY));

        return new PointSourceFrameState
        {
            Origin = origin,
            AxisX = new Vector3D(axisX.X, axisX.Y, axisX.Z),
            AxisY = new Vector3D(axisY.X, axisY.Y, axisY.Z),
            AxisZ = new Vector3D(axisZ.X, axisZ.Y, axisZ.Z),
        };
    }

    private static Vector3 ToVector3(Vector3D value) => new((float)value.X, (float)value.Y, (float)value.Z);
}
