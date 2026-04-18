using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record PointSourceProjectionParameters(
    Point3 SourceOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY) : IProjectionParameters;
