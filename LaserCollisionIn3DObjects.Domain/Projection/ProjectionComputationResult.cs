using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionComputationResult
{
    public required string MethodId { get; init; }

    public PointLightSource? PointLightSource { get; init; }

    public required IReadOnlyList<ProjectionRay> Rays { get; init; }

    public Point3? SourcePoint => PointLightSource?.Position;
}
