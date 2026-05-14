using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed class ConicalFrustumLightSource : AxisymmetricLightSource
{
    public ConicalFrustumLightSource(string name, Frame3D frame, float radiusStart, float radiusEnd, float length, int rayCount, float tiltWeight = 0.1f, Vector3? tiltPointLocal = null)
        : base(name, frame, AxisymmetricSourceKind.ConicalFrustum, new ConicalFrustumSourceProfile(radiusStart, radiusEnd, length), rayCount, tiltWeight, tiltPointLocal)
    {
    }
}
