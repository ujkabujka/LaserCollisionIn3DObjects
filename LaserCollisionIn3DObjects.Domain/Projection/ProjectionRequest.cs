using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class ProjectionRequest
{
    public required IReadOnlyList<Point3> HolePoints { get; init; }

    public required IProjectionParameters Parameters { get; init; }

    public IProgress<ProjectionProgress>? Progress { get; init; }
}
