using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Persistence;

public static class BaseOrientationPersistence
{
    private const float ZeroEpsilon = 1e-6f;

    public static Quaternion FromComponents(float? x, float? y, float? z, float? w)
    {
        if (x is null || y is null || z is null || w is null)
        {
            return Quaternion.Identity;
        }

        var candidate = new Quaternion(x.Value, y.Value, z.Value, w.Value);
        if (candidate.LengthSquared() <= ZeroEpsilon)
        {
            return Quaternion.Identity;
        }

        return Quaternion.Normalize(candidate);
    }
}
