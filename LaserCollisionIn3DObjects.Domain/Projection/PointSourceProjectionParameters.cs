using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record PointSourceProjectionParameters(
    Point3 PointSourceOrigin,
    Point3 BeamOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY) : IProjectionParameters;
