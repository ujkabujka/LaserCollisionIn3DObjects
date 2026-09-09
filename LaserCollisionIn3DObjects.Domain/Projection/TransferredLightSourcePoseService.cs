using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

/// <summary>Transforms authoritative source rays through an old-frame/local/new-frame rigid mapping.</summary>
public static class TransferredLightSourcePoseService
{
    public static Quaternion GetOrientation(PointSourceFrameState frame)
    {
        var matrix = new Matrix4x4(
            (float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z, 0,
            (float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z, 0,
            (float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z, 0,
            0, 0, 0, 1);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
    }

    public static PointSourceFrameState CreateFrame(Vector3 origin, Quaternion orientation)
    {
        orientation = Quaternion.Normalize(orientation);
        var x = Vector3.Normalize(Vector3.Transform(Vector3.UnitX, orientation));
        var y = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, orientation));
        var z = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, orientation));
        return new PointSourceFrameState { Origin = new Point3(origin.X, origin.Y, origin.Z), AxisX = ToVector(x), AxisY = ToVector(y), AxisZ = ToVector(z) };
    }

    public static Ray3D TransformRay(Ray3D ray, PointSourceFrameState oldFrame, PointSourceFrameState newFrame)
    {
        var localOrigin = WorldToLocal(ray.Origin - ToVector(oldFrame.Origin), oldFrame);
        var localDirection = WorldToLocal(ray.Direction, oldFrame);
        var origin = ToVector(newFrame.Origin) + LocalToWorld(localOrigin, newFrame);
        var direction = LocalToWorld(localDirection, newFrame);
        if (!IsFinite(origin) || !IsFinite(direction) || direction.LengthSquared() <= 0f)
            throw new ArgumentException("Source frames and rays must be finite and ray directions non-zero.");
        return new Ray3D(origin, Vector3.Normalize(direction));
    }

    private static Vector3 WorldToLocal(Vector3 v, PointSourceFrameState f) => new(Vector3.Dot(v, ToVector(f.AxisX)), Vector3.Dot(v, ToVector(f.AxisY)), Vector3.Dot(v, ToVector(f.AxisZ)));
    private static Vector3 LocalToWorld(Vector3 v, PointSourceFrameState f) => ToVector(f.AxisX) * v.X + ToVector(f.AxisY) * v.Y + ToVector(f.AxisZ) * v.Z;
    private static Vector3 ToVector(Point3 p) => new((float)p.X, (float)p.Y, (float)p.Z);
    private static Vector3 ToVector(Vector3D v) => new((float)v.X, (float)v.Y, (float)v.Z);
    private static Vector3D ToVector(Vector3 v) => new(v.X, v.Y, v.Z);
    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
