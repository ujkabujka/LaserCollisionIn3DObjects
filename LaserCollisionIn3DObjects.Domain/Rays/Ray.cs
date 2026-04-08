using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Rays;

public sealed record Ray(Point3 Origin, Vector3D Direction);
