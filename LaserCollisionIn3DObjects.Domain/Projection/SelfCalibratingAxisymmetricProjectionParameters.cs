using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

<<<<<<<< HEAD:LaserCollisionIn3DObjects.Domain/Projection/SelfCalibratingAxisymmetricProjectionParameters.cs
public sealed record SelfCalibratingAxisymmetricProjectionParameters(
========
public sealed record LeastSquaresAxisymmetricAlignmentProjectionParameters(
>>>>>>>> codex/rename-least-squares-cylindrical-classes:LaserCollisionIn3DObjects.Domain/Projection/LeastSquaresAxisymmetricAlignmentProjectionParameters.cs
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    AxisymmetricSourceProfileDefinition ProfileDefinition,
    Point3 LocalTiltPoint) : IProjectionParameters;
