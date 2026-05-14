using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public static class RayAngleMath
{
    private const float Epsilon = 1e-7f;

    public static Vector3 NormalizeNonZero(Vector3 value, string paramName)
    {
        if (value.LengthSquared() <= Epsilon)
        {
            throw new ArgumentException("Vector must be non-zero.", paramName);
        }

        return Vector3.Normalize(value);
    }

    public static double CalculatePolarDegrees(Vector3 localAxisX, Vector3 direction)
    {
        var axisX = NormalizeNonZero(localAxisX, nameof(localAxisX));
        var normalizedDirection = NormalizeNonZero(direction, nameof(direction));

        var dot = Math.Clamp(Vector3.Dot(axisX, normalizedDirection), -1f, 1f);
        return Math.Acos(dot) * (180.0 / Math.PI);
    }

    public static double CalculateAzimuthDegrees(Vector3 localAxisX, Vector3 localAxisY, Vector3 localAxisZ, Vector3 direction)
    {
        var axisX = NormalizeNonZero(localAxisX, nameof(localAxisX));
        var axisY = NormalizeNonZero(localAxisY, nameof(localAxisY));
        var axisZ = NormalizeNonZero(localAxisZ, nameof(localAxisZ));
        var normalizedDirection = NormalizeNonZero(direction, nameof(direction));

        var transverse = normalizedDirection - (Vector3.Dot(normalizedDirection, axisX) * axisX);
        if (transverse.LengthSquared() <= Epsilon)
        {
            // Convention: rays almost parallel to local X use azimuth 0°.
            return 0d;
        }

        var transverseUnit = Vector3.Normalize(transverse);
        var yComponent = Vector3.Dot(transverseUnit, axisY);
        var zComponent = Vector3.Dot(transverseUnit, axisZ);

        var azimuth = Math.Atan2(zComponent, yComponent) * (180.0 / Math.PI);
        if (azimuth < 0)
        {
            azimuth += 360;
        }

        return azimuth;
    }
}
