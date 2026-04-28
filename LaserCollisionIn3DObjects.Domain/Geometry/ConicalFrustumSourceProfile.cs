using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed class ConicalFrustumSourceProfile : IAxisymmetricSourceProfile
{
    private readonly float _radiusSlope;

    public ConicalFrustumSourceProfile(float radiusStart, float radiusEnd, float length)
    {
        RadiusStart = CylindricalSourceProfile.EnsurePositive(radiusStart, nameof(radiusStart));
        RadiusEnd = CylindricalSourceProfile.EnsurePositive(radiusEnd, nameof(radiusEnd));
        Length = CylindricalSourceProfile.EnsurePositive(length, nameof(length));
        _radiusSlope = (RadiusEnd - RadiusStart) / Length;
    }

    public float RadiusStart { get; }
    public float RadiusEnd { get; }
    public float Length { get; }

    public float RadiusAt(float u)
    {
        CylindricalSourceProfile.EnsureUInRange(u, Length);
        return RadiusStart + (_radiusSlope * u);
    }

    public float RadiusDerivativeAt(float u)
    {
        CylindricalSourceProfile.EnsureUInRange(u, Length);
        return _radiusSlope;
    }

    public Vector3 EvaluateSurfacePoint(float u, float theta)
    {
        var radius = RadiusAt(u);
        return new Vector3(u, radius * MathF.Cos(theta), radius * MathF.Sin(theta));
    }

    public Vector3 EvaluateBaseDirection(float u, float theta)
    {
        var derivative = RadiusDerivativeAt(u);
        return Vector3.Normalize(new Vector3(-derivative, MathF.Cos(theta), MathF.Sin(theta)));
    }
}
