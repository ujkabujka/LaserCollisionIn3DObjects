using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record CylindricalProjectionPoint(
    Point3 HolePoint,
    Point3 SourceSurfacePoint,
    Vector3D RayDirection,
    Point3 RayOrigin);

public sealed class CylindricalProjectionState
{
    public required PointSourceFrameState SourceFrame { get; init; }

    public required double Radius { get; init; }

    public required double Length { get; init; }

    public required IReadOnlyList<CylindricalProjectionPoint> Points { get; init; }
}
