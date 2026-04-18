using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record ProjectionRay(Ray3D Ray, Point3 TargetHolePoint);
