using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

/// <summary>Transforms points and directions between world and source-local coordinates.</summary>
public static class PointSourceFrameTransforms
{
    public static Point3 WorldToLocal(Point3 world, PointSourceFrameState frame)
    {
        var dx = world.X - frame.Origin.X;
        var dy = world.Y - frame.Origin.Y;
        var dz = world.Z - frame.Origin.Z;

        return new Point3(
            (dx * frame.AxisX.X) + (dy * frame.AxisX.Y) + (dz * frame.AxisX.Z),
            (dx * frame.AxisY.X) + (dy * frame.AxisY.Y) + (dz * frame.AxisY.Z),
            (dx * frame.AxisZ.X) + (dy * frame.AxisZ.Y) + (dz * frame.AxisZ.Z));
    }

    public static Point3 LocalToWorld(Point3 local, PointSourceFrameState frame)
        => new(
            frame.Origin.X + (local.X * frame.AxisX.X) + (local.Y * frame.AxisY.X) + (local.Z * frame.AxisZ.X),
            frame.Origin.Y + (local.X * frame.AxisX.Y) + (local.Y * frame.AxisY.Y) + (local.Z * frame.AxisZ.Y),
            frame.Origin.Z + (local.X * frame.AxisX.Z) + (local.Y * frame.AxisY.Z) + (local.Z * frame.AxisZ.Z));

    public static Point3 LocalToWorld(Vector3 local, PointSourceFrameState frame)
        => LocalToWorld(new Point3(local.X, local.Y, local.Z), frame);

    public static Vector3D LocalDirectionToWorld(Vector3D local, PointSourceFrameState frame)
        => new(
            (local.X * frame.AxisX.X) + (local.Y * frame.AxisY.X) + (local.Z * frame.AxisZ.X),
            (local.X * frame.AxisX.Y) + (local.Y * frame.AxisY.Y) + (local.Z * frame.AxisZ.Y),
            (local.X * frame.AxisX.Z) + (local.Y * frame.AxisY.Z) + (local.Z * frame.AxisZ.Z));

    public static Vector3D LocalDirectionToWorld(Vector3 local, PointSourceFrameState frame)
        => LocalDirectionToWorld(new Vector3D(local.X, local.Y, local.Z), frame);
}
