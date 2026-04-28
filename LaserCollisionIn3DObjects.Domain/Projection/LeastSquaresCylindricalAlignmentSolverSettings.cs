namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresCylindricalAlignmentSolverSettings
{
    public static LeastSquaresCylindricalAlignmentSolverSettings Default { get; } = new();

    public int MaxIterations { get; init; } = 20;
    public int PointRefinementIterations { get; init; } = 12;
    public int LambdaRefinementIterations { get; init; } = 8;
    public double PointStepScale { get; init; } = 0.8;
    public double LambdaStepScale { get; init; } = 0.5;
    public double ConvergenceTolerance { get; init; } = 1e-7;
    public double ProgressAngularThresholdDegrees { get; init; } = 0.01;
}
