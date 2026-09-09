using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

internal static class DirectAxisymmetricProjectionInitializer
{
    private const double LocalRangeTolerance = 1e-9;
    private const float DirectionLengthTolerance = 1e-8f;

    public static IReadOnlyList<AxisymmetricProjectionPoint> Initialize(
        IReadOnlyList<Point3> worldHolePoints,
        PointSourceFrameState frame,
        IAxisymmetricSourceProfile profile)
    {
        if (worldHolePoints.Count == 0)
        {
            return [];
        }

        var holesLocal = worldHolePoints.Select(hole => PointSourceFrameTransforms.WorldToLocal(hole, frame)).ToList();
        var xMin = holesLocal.Min(point => point.X);
        var xMax = holesLocal.Max(point => point.X);
        var xRange = xMax - xMin;

        var points = new List<AxisymmetricProjectionPoint>(worldHolePoints.Count);

        for (var i = 0; i < worldHolePoints.Count; i++)
        {
            var holeWorld = worldHolePoints[i];
            var holeLocal = holesLocal[i];

            var u = xRange <= LocalRangeTolerance
                ? profile.Length / 2d
                : ((holeLocal.X - xMin) / xRange) * profile.Length;
            u = Math.Clamp(u, 0d, profile.Length);

            var theta = Math.Atan2(holeLocal.Z, holeLocal.Y);
            var surfaceLocal = profile.EvaluateSurfacePoint((float)u, (float)theta);
            var sourceWorld = PointSourceFrameTransforms.LocalToWorld(surfaceLocal, frame);

            var directionVector = new Vector3((float)(holeWorld.X - sourceWorld.X), (float)(holeWorld.Y - sourceWorld.Y), (float)(holeWorld.Z - sourceWorld.Z));
            Vector3D rayDirection;
            if (directionVector.LengthSquared() <= DirectionLengthTolerance)
            {
                rayDirection = NormalizeWorldDirection(PointSourceFrameTransforms.LocalDirectionToWorld(profile.EvaluateBaseDirection((float)u, (float)theta), frame));
            }
            else
            {
                var normalized = Vector3.Normalize(directionVector);
                rayDirection = new Vector3D(normalized.X, normalized.Y, normalized.Z);
            }

            points.Add(new AxisymmetricProjectionPoint(holeWorld, sourceWorld, rayDirection, sourceWorld)
            {
                LocalU = u,
                LocalTheta = theta,
                UnwrappedU = u,
                UnwrappedV = profile.RadiusAt((float)u) * theta,
            });
        }

        return points;
    }

    private static Vector3D NormalizeWorldDirection(Vector3D worldDirection)
    {
        var world = new Vector3((float)worldDirection.X, (float)worldDirection.Y, (float)worldDirection.Z);

        if (world.LengthSquared() <= DirectionLengthTolerance)
        {
            throw new InvalidOperationException("Unable to derive a valid ray direction for direct linear projection.");
        }

        world = Vector3.Normalize(world);
        return new Vector3D(world.X, world.Y, world.Z);
    }
}
