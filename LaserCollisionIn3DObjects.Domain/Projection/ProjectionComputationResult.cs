namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionComputationResult
{
    public required string MethodId { get; init; }

    public required PointSourceFrameState SourceFrame { get; init; }

    public required IReadOnlyList<ProjectionRay> Rays { get; init; }
}
