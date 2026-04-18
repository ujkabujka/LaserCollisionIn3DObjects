using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record PointSourceProjectionParameters(Point3 SourcePoint) : IProjectionParameters;
