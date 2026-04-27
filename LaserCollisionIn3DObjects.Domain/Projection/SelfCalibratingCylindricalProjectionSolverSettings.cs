namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class SelfCalibratingCylindricalProjectionSolverSettings
{
    public static SelfCalibratingCylindricalProjectionSolverSettings Default { get; } = new();

    public IReadOnlyList<double> KappaCandidates { get; init; } = [0, 0.025, 0.05, 0.1, 0.2, 0.35, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 5.0];
    public int AxialSamples { get; init; } = 33;
    public int AngularSamples { get; init; } = 72;
    public int RefinementIterations { get; init; } = 10;
    public double RegularityWeight { get; init; } = 0.05;
}
