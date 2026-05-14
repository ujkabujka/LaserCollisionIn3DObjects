using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record AxisymmetricSourceProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    AxisymmetricSourceProfileDefinition ProfileDefinition) : IProjectionParameters;

public sealed record SelfCalibratingAxisymmetricProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    AxisymmetricSourceProfileDefinition ProfileDefinition,
    Point3 LocalTiltPoint) : IProjectionParameters;

public sealed record LeastSquaresAxisymmetricAlignmentProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    AxisymmetricSourceProfileDefinition ProfileDefinition,
    Point3 LocalTiltPoint,
    LeastSquaresAxisymmetricAlignmentSolverSettings SolverSettings) : IProjectionParameters;
