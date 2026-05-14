using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed class CircularOgiveLightSource : AxisymmetricLightSource
{
    public CircularOgiveLightSource(string name, Frame3D frame, float radiusStart, float radiusEnd, float length, float arcRadius, OgiveCurvatureDirection curvatureDirection, int rayCount, float tiltWeight = 0.1f, Vector3? tiltPointLocal = null)
        : base(name, frame, AxisymmetricSourceKind.CircularOgive, new CircularOgiveSourceProfile(radiusStart, radiusEnd, length, arcRadius, curvatureDirection), rayCount, tiltWeight, tiltPointLocal)
    {
    }
}
