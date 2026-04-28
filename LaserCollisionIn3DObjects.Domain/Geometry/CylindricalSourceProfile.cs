using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed class CylindricalSourceProfile : IAxisymmetricSourceProfile
{
    public CylindricalSourceProfile(float radius, float length)
    {
        Radius = EnsurePositive(radius, nameof(radius));
        Length = EnsurePositive(length, nameof(length));
    }

    public float Radius { get; }

    public float Length { get; }

    public float RadiusAt(float u)
    {
        EnsureUInRange(u, Length);
        return Radius;
    }

    public float RadiusDerivativeAt(float u)
    {
        EnsureUInRange(u, Length);
        return 0f;
    }

    public Vector3 EvaluateSurfacePoint(float u, float theta)
    {
        var radius = RadiusAt(u);
        return new Vector3(u, radius * MathF.Cos(theta), radius * MathF.Sin(theta));
    }

    public Vector3 EvaluateBaseDirection(float u, float theta)
    {
        _ = RadiusDerivativeAt(u);
        return Vector3.Normalize(new Vector3(0f, MathF.Cos(theta), MathF.Sin(theta)));
    }

    internal static float EnsurePositive(float value, string paramName)
    {
        if (value <= 0f)
        {
            throw new ArgumentException("Value must be greater than zero.", paramName);
        }

        return value;
    }

    internal static void EnsureUInRange(float u, float length)
    {
        if (u < 0f || u > length)
        {
            throw new ArgumentOutOfRangeException(nameof(u), "u must be inside [0, Length].");
        }
    }
}
