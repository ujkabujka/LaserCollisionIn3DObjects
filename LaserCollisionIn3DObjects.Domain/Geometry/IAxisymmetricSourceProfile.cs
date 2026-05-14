using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public interface IAxisymmetricSourceProfile
{
    float Length { get; }

    float RadiusAt(float u);

    float RadiusDerivativeAt(float u);

    Vector3 EvaluateSurfacePoint(float u, float theta);

    Vector3 EvaluateBaseDirection(float u, float theta);
}
