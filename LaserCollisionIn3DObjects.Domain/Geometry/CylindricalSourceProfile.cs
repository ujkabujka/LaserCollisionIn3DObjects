using System.Numerics;
using System.Reflection.Metadata;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public class AxisymmetricSourceProfile : IAxisymmetricSourceProfile
{
    public const double lower = 1e-4;
    public AxisymmetricSourceProfile(float radius, float length)
    {
        Radius = EnsurePositive(radius, nameof(radius));
        Length = EnsurePositive(length, nameof(length));
    }

    public float Radius { get; }
    public float Length { get; }
    public float RadiusAt(float u) { EnsureUInRange(u, Length); return Radius; }
    public float RadiusDerivativeAt(float u) { EnsureUInRange(u, Length); return 0f; }
    public Vector3 EvaluateSurfacePoint(float u, float theta) => new(u, RadiusAt(u) * MathF.Cos(theta), RadiusAt(u) * MathF.Sin(theta));
    public Vector3 EvaluateBaseDirection(float u, float theta) => Vector3.Normalize(new Vector3(0f, MathF.Cos(theta), MathF.Sin(theta)));

    internal static float EnsurePositive(float value, string paramName) => value <= 0f ? throw new ArgumentException("Value must be greater than zero.", paramName) : value;
    internal static void EnsureUInRange(float u, float length) { 
        if (u < -lower || u > length + lower) 
            throw new ArgumentOutOfRangeException(nameof(u), "u must be inside [0, Length]."); 
    }
}

public sealed class CylindricalSourceProfile : AxisymmetricSourceProfile
{
    public CylindricalSourceProfile(float radius, float length) : base(radius, length) { }
}
