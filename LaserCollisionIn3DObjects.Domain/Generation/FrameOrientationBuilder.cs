using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Generation;

public static class FrameOrientationBuilder
{
    private const float AngleEpsilon = 1e-6f;

    public static Quaternion ApplyLocalEulerDegrees(
        Quaternion baseOrientation,
        float rotationXDegrees,
        float rotationYDegrees,
        float rotationZDegrees)
    {
        var orientation = Quaternion.Normalize(baseOrientation);
        orientation = ApplyLocalAxisRotation(orientation, Vector3.UnitX, DegreesToRadians(rotationXDegrees));
        orientation = ApplyLocalAxisRotation(orientation, Vector3.UnitY, DegreesToRadians(rotationYDegrees));
        orientation = ApplyLocalAxisRotation(orientation, Vector3.UnitZ, DegreesToRadians(rotationZDegrees));
        return orientation;
    }

    public static Quaternion CreateFacingOriginOrientation(Vector3 position)
    {
        var horizontalDirectionToOrigin = new Vector3(-position.X, -position.Y, 0f);

        if (horizontalDirectionToOrigin.LengthSquared() <= AngleEpsilon)
        {
            return Quaternion.Identity;
        }

        horizontalDirectionToOrigin = Vector3.Normalize(horizontalDirectionToOrigin);
        //var yawRadians = MathF.Acos(Vector3.Dot(Vector3.UnitX, horizontalDirectionToOrigin));
        var yawRadians = MathF.Atan2(horizontalDirectionToOrigin.Y, horizontalDirectionToOrigin.X);
        return Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yawRadians);
    }

    private static Quaternion ApplyLocalAxisRotation(Quaternion currentOrientation, Vector3 localAxis, float angleRadians)
    {
        if (MathF.Abs(angleRadians) <= AngleEpsilon)
        {
            return currentOrientation;
        }

        var worldAxis = Vector3.Normalize(Vector3.Transform(localAxis, currentOrientation));
        var delta = Quaternion.CreateFromAxisAngle(worldAxis, angleRadians);
        return Quaternion.Normalize(delta * currentOrientation);
    }

    public static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }

    public static float RadiansToDegrees(float radians)
    {
        return radians * (180f / MathF.PI);
    }

    public static double DegreesToRadians(double degrees)
    {
        return degrees * (MathF.PI / 180.0);
    }
}
