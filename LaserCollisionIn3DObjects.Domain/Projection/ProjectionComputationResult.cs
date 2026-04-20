using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionComputationResult
{
    public required string MethodId { get; init; }

    public required Point3 PointSourceOrigin { get; init; }

    public required PointSourceFrameState SourceFrame { get; init; }

    public required IReadOnlyList<ProjectionRay> Rays { get; init; }

    public PointLaserSourceState ToPointLaserSource() =>
        new()
        {
            Origin = SourceFrame.Origin,
            AxisX = SourceFrame.AxisX,
            AxisY = SourceFrame.AxisY,
            AxisZ = SourceFrame.AxisZ,
            Rays = Rays,
        };
}
