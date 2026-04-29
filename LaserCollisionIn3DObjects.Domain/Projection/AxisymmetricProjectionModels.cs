using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record AxisymmetricSourceProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    double Radius,
    double Length) : IProjectionParameters;

public sealed record SelfCalibratingAxisymmetricProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    double Radius,
    double Length,
    Point3 LocalTiltPoint) : IProjectionParameters;

public sealed record LeastSquaresAxisymmetricAlignmentProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    double Radius,
    double Length,
    Point3 LocalTiltPoint) : IProjectionParameters;

public sealed record AxisymmetricSourceProfileDefinition(
    double Radius,
    double Length,
    string ProfileKind,
    IReadOnlyList<HybridAxisymmetricSourceSegmentDefinition>? HybridSegments = null);

