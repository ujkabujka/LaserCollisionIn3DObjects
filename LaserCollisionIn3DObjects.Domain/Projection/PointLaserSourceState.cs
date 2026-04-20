using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class PointLaserSourceState
{
    public required Point3 Origin { get; init; }
    public required Vector3D AxisX { get; init; }
    public required Vector3D AxisY { get; init; }
    public required Vector3D AxisZ { get; init; }
    public required IReadOnlyList<ProjectionRay> Rays { get; init; }
}
