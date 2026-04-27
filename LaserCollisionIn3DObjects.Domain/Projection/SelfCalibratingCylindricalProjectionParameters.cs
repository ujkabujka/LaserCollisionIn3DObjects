using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record SelfCalibratingCylindricalProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    double Radius,
    double Length,
    Point3 LocalTiltPoint) : IProjectionParameters;
