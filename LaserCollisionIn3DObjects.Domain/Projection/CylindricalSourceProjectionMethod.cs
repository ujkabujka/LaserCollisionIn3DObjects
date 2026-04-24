using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class CylindricalSourceProjectionMethod : IProjectionMethod
{
    private const double ZeroTolerance = 1e-9;

    public ProjectionMethodMetadata Metadata { get; } = new(
        ProjectionMethodIds.CylindricalSource,
        "User-defined cylindrical source",
        "Reconstructs one source-surface point per hole by normalizing local X into source length and projecting local YZ onto radius.");

    public ProjectionComputationResult Execute(ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Parameters is not CylindricalSourceProjectionParameters parameters)
        {
            throw new ArgumentException("Cylindrical-source projection requires cylindrical parameters.", nameof(request));
        }

        if (request.HolePoints is null || request.HolePoints.Count == 0)
        {
            throw new ArgumentException("Projection requires at least one hole point.", nameof(request));
        }

        if (parameters.Radius <= 0d)
        {
            throw new ArgumentException("Cylinder radius must be greater than zero.", nameof(request));
        }

        if (parameters.Length <= 0d)
        {
            throw new ArgumentException("Cylinder length must be greater than zero.", nameof(request));
        }

        var sourceFrame = PointSourceFrameBuilder.Build(
            parameters.SourceFrameOrigin,
            parameters.SourceFrameX,
            parameters.SourceFrameY);

        var localHolePoints = request.HolePoints
            .Select(holePoint => ToLocal(holePoint, sourceFrame))
            .ToList();

        var xMin = localHolePoints.Min(point => point.X);
        var xMax = localHolePoints.Max(point => point.X);
        var span = xMax - xMin;
        if (Math.Abs(span) <= ZeroTolerance)
        {
            throw new ArgumentException("Hole points cannot be normalized: all transformed local X coordinates are equal.", nameof(request));
        }

        var reconstructedPoints = new List<CylindricalProjectionPoint>(request.HolePoints.Count);
        for (var i = 0; i < request.HolePoints.Count; i++)
        {
            var localHole = localHolePoints[i];
            var normalizedX = (localHole.X - xMin) * (parameters.Length / span);

            var radialLength = Math.Sqrt((localHole.Y * localHole.Y) + (localHole.Z * localHole.Z));
            if (radialLength <= ZeroTolerance)
            {
                throw new ArgumentException(
                    $"Hole point at index {i} collapses to local radial (0,0). Unable to reconstruct cylindrical surface point.",
                    nameof(request));
            }

            var radialScale = parameters.Radius / radialLength;
            var reconstructedLocal = new Point3(
                normalizedX,
                localHole.Y * radialScale,
                localHole.Z * radialScale);

            var surfaceWorld = ToWorld(reconstructedLocal, sourceFrame);
            var holeWorld = request.HolePoints[i];

            var directionVector = new Vector3(
                (float)(holeWorld.X - surfaceWorld.X),
                (float)(holeWorld.Y - surfaceWorld.Y),
                (float)(holeWorld.Z - surfaceWorld.Z));
            if (directionVector.LengthSquared() <= 0f)
            {
                throw new ArgumentException($"Hole point at index {i} coincides with reconstructed source point.", nameof(request));
            }

            var direction = Vector3.Normalize(directionVector);
            reconstructedPoints.Add(new CylindricalProjectionPoint(
                holeWorld,
                surfaceWorld,
                new Vector3D(direction.X, direction.Y, direction.Z),
                surfaceWorld));
        }

        return new ProjectionComputationResult
        {
            MethodId = Metadata.Id,
            SourceFrame = sourceFrame,
            Rays = Array.Empty<ProjectionRay>(),
            CylindricalSource = new CylindricalProjectionState
            {
                SourceFrame = sourceFrame,
                Radius = parameters.Radius,
                Length = parameters.Length,
                Points = reconstructedPoints,
            },
        };
    }

    private static Point3 ToLocal(Point3 worldPoint, PointSourceFrameState frame)
    {
        var deltaX = worldPoint.X - frame.Origin.X;
        var deltaY = worldPoint.Y - frame.Origin.Y;
        var deltaZ = worldPoint.Z - frame.Origin.Z;

        return new Point3(
            Dot(deltaX, deltaY, deltaZ, frame.AxisX),
            Dot(deltaX, deltaY, deltaZ, frame.AxisY),
            Dot(deltaX, deltaY, deltaZ, frame.AxisZ));
    }

    private static Point3 ToWorld(Point3 localPoint, PointSourceFrameState frame)
    {
        return new Point3(
            frame.Origin.X + (localPoint.X * frame.AxisX.X) + (localPoint.Y * frame.AxisY.X) + (localPoint.Z * frame.AxisZ.X),
            frame.Origin.Y + (localPoint.X * frame.AxisX.Y) + (localPoint.Y * frame.AxisY.Y) + (localPoint.Z * frame.AxisZ.Y),
            frame.Origin.Z + (localPoint.X * frame.AxisX.Z) + (localPoint.Y * frame.AxisY.Z) + (localPoint.Z * frame.AxisZ.Z));
    }

    private static double Dot(double dx, double dy, double dz, Vector3D axis)
    {
        return (dx * axis.X) + (dy * axis.Y) + (dz * axis.Z);
    }
}
