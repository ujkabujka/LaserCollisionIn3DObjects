using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class AxisymmetricSourceProjectionMethod : IProjectionMethod
{
    private const double LocalRangeTolerance = 1e-9;
    private const float DirectionLengthTolerance = 1e-8f;

    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.AxisymmetricSource,
        "Direct linear projection method",
        "Directly maps local hole coordinates to the selected axisymmetric source geometry using axial normalization and angular projection. Works with cylinder, conical frustum, circular ogive, and hybrid geometries.");

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        var p = (AxisymmetricSourceProjectionParameters)request.Parameters;
        var profile = p.ProfileDefinition.BuildProfile();
        var frame = PointSourceFrameBuilder.Build(p.SourceFrameOrigin, p.SourceFrameX, p.SourceFrameY);

        if (request.HolePoints.Count == 0)
        {
            return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, ProfileDefinition = p.ProfileDefinition, Radius = profile.RadiusAt(0), Length = profile.Length, Points = [] } };
        }

        var holesLocal = request.HolePoints.Select(hole => ToLocal(hole, frame)).ToList();
        var xMin = holesLocal.Min(point => point.X);
        var xMax = holesLocal.Max(point => point.X);
        var xRange = xMax - xMin;

        var points = new List<AxisymmetricProjectionPoint>(request.HolePoints.Count);

        for (var i = 0; i < request.HolePoints.Count; i++)
        {
            var holeWorld = request.HolePoints[i];
            var holeLocal = holesLocal[i];

            var u = xRange <= LocalRangeTolerance
                ? profile.Length / 2d
                : ((holeLocal.X - xMin) / xRange) * profile.Length;
            u = Math.Clamp(u, 0d, profile.Length);

            var theta = Math.Atan2(holeLocal.Z, holeLocal.Y);
            var surfaceLocal = profile.EvaluateSurfacePoint((float)u, (float)theta);
            var sourceWorld = ToWorld(surfaceLocal, frame);

            var directionVector = new Vector3((float)(holeWorld.X - sourceWorld.X), (float)(holeWorld.Y - sourceWorld.Y), (float)(holeWorld.Z - sourceWorld.Z));
            Vector3D rayDirection;
            if (directionVector.LengthSquared() <= DirectionLengthTolerance)
            {
                rayDirection = ToWorldDirection(profile.EvaluateBaseDirection((float)u, (float)theta), frame);
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

        return new ProjectionComputationResult { MethodId = Metadata.Id, SourceFrame = frame, Rays = Array.Empty<ProjectionRay>(), AxisymmetricSource = new AxisymmetricProjectionState { SourceFrame = frame, ProfileDefinition = p.ProfileDefinition, Radius = profile.RadiusAt(0), Length = profile.Length, Points = points } };
    }

    private static Point3 ToLocal(Point3 world, PointSourceFrameState frame)
    {
        var dx = world.X - frame.Origin.X;
        var dy = world.Y - frame.Origin.Y;
        var dz = world.Z - frame.Origin.Z;

        var localX = (dx * frame.AxisX.X) + (dy * frame.AxisX.Y) + (dz * frame.AxisX.Z);
        var localY = (dx * frame.AxisY.X) + (dy * frame.AxisY.Y) + (dz * frame.AxisY.Z);
        var localZ = (dx * frame.AxisZ.X) + (dy * frame.AxisZ.Y) + (dz * frame.AxisZ.Z);
        return new Point3(localX, localY, localZ);
    }

    private static Point3 ToWorld(Vector3 local, PointSourceFrameState frame)
    {
        return new Point3(
            frame.Origin.X + (local.X * frame.AxisX.X) + (local.Y * frame.AxisY.X) + (local.Z * frame.AxisZ.X),
            frame.Origin.Y + (local.X * frame.AxisX.Y) + (local.Y * frame.AxisY.Y) + (local.Z * frame.AxisZ.Y),
            frame.Origin.Z + (local.X * frame.AxisX.Z) + (local.Y * frame.AxisY.Z) + (local.Z * frame.AxisZ.Z));
    }

    private static Vector3D ToWorldDirection(Vector3 localDirection, PointSourceFrameState frame)
    {
        var world = new Vector3(
            (float)((localDirection.X * frame.AxisX.X) + (localDirection.Y * frame.AxisY.X) + (localDirection.Z * frame.AxisZ.X)),
            (float)((localDirection.X * frame.AxisX.Y) + (localDirection.Y * frame.AxisY.Y) + (localDirection.Z * frame.AxisZ.Y)),
            (float)((localDirection.X * frame.AxisX.Z) + (localDirection.Y * frame.AxisY.Z) + (localDirection.Z * frame.AxisZ.Z)));

        if (world.LengthSquared() <= DirectionLengthTolerance)
        {
            throw new InvalidOperationException("Unable to derive a valid ray direction for direct linear projection.");
        }

        world = Vector3.Normalize(world);
        return new Vector3D(world.X, world.Y, world.Z);
    }
}
