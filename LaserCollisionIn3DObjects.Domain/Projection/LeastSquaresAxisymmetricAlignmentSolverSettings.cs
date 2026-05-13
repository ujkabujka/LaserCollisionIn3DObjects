namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresAxisymmetricAlignmentSolverSettings
{
    public static LeastSquaresAxisymmetricAlignmentSolverSettings Default { get; } = new();

    public int MaxIterations { get; init; } = 20;
    public double ConvergenceTolerance { get; init; } = 1e-7;
    public double PointStepScale { get; init; } = 0.05;
    public double ThetaStepScale { get; init; } = 0.05;
    public double LambdaStepScale { get; init; } = 0.25;
    public double FiniteDifferenceRelativeStep { get; init; } = 1e-5;
    public double BacktrackingFactor { get; init; } = 0.5;
    public int MaxBacktrackingAttempts { get; init; } = 10;
    public double ProgressAngularThresholdDegrees { get; init; } = 0.01;
}
