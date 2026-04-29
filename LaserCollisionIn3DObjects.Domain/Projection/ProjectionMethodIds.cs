namespace LaserCollisionIn3DObjects.Domain.Projection;

public static class ProjectionMethodIds
{
    public const string PointSource = "point-source";
    public const string AxisymmetricSource = "axisymmetric-source";
    public const string SelfCalibratingAxisymmetricSource = "self-calibrating-axisymmetric-source";
    public const string LeastSquaresAxisymmetricAlignmentSource = "least-squares-axisymmetric-alignment-source";

    // Backward-compatible legacy identifiers.
    public const string CylindricalSource = AxisymmetricSource;
    public const string SelfCalibratingCylindricalSource = SelfCalibratingAxisymmetricSource;
    public const string LeastSquaresCylindricalAlignmentSource = LeastSquaresAxisymmetricAlignmentSource;
    public const string CylindricalSource = "cylindrical-source";
    public const string SelfCalibratingAxisymmetricSource = "self-calibrating-axisymmetric-source";
    public const string LeastSquaresCylindricalAlignmentSource = "least-squares-cylindrical-alignment-source";
}
