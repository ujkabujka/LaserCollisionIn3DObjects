using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Generation;

public sealed class AxisymmetricRayGenerator
{
    public List<Ray3D> Generate(AxisymmetricLightSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var rays = new List<Ray3D>(source.RayCount);
        var rows = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(source.RayCount)));
        var columns = Math.Max(1, (int)MathF.Ceiling(source.RayCount / (float)rows));

        for (var i = 0; i < source.RayCount; i++)
        {
            var row = i / columns;
            var column = i % columns;

            var u = source.Profile.Length * (row / (float)rows);
            var theta = 2f * MathF.PI * ((column + (0.5f * (row % 2))) / columns);

            var localOrigin = source.Profile.EvaluateSurfacePoint(u, theta);
            var baseDirection = source.Profile.EvaluateBaseDirection(u, theta);
            var localDirection = GetTiltedDirection(baseDirection, localOrigin, source.TiltWeight, source.TiltPointLocal);

            rays.Add(new Ray3D(
                source.Frame.TransformPointToWorld(localOrigin),
                Vector3.Normalize(source.Frame.TransformDirectionToWorld(localDirection))));
        }

        return rays;
    }

    private static Vector3 GetTiltedDirection(Vector3 baseDirection, Vector3 localOrigin, float tiltWeight, Vector3 tiltPointLocal)
    {
        if (tiltWeight <= 0f)
        {
            return baseDirection;
        }

        var tiltVector = localOrigin - tiltPointLocal;
        return Vector3.Normalize(baseDirection + (tiltWeight * tiltVector));
    }
}
