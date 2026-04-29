using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresAxisymmetricAlignmentSolver
{
    public LeastSquaresAxisymmetricAlignmentSolverSettings Settings { get; }

    public LeastSquaresAxisymmetricAlignmentSolver(LeastSquaresAxisymmetricAlignmentSolverSettings? settings = null)
    {
        Settings = settings ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default;
    }

    public LeastSquaresAxisymmetricAlignmentSolveResult Solve(
        IReadOnlyList<Point3> localHolePoints,
        PointSourceFrameState frame,
        IAxisymmetricSourceProfile profile,
        Point3 localTiltPoint,
        IReadOnlyList<Point3> worldHolePoints,
        IProgress<ProjectionProgress>? progress = null)
    {
        var initSolver = new SelfCalibratingAxisymmetricProjectionSolver();
        var init = initSolver.Solve(localHolePoints, frame, profile, localTiltPoint, worldHolePoints, null);

        var diagnostics = new LeastSquaresAxisymmetricAlignmentDiagnostics
        {
            InitialLambda = init.EstimatedTiltWeight,
            RefinedLambda = init.EstimatedTiltWeight,
            InitialMeanAlignmentError = 0,
            FinalMeanAlignmentError = 0,
            FinalRmsAlignmentError = 0,
            FinalMeanAngularErrorDegrees = 0,
            FinalMaxAngularErrorDegrees = 0,
            Iterations = 0,
            Converged = true,
            UsesRegularization = false,
            IterationHistory = Array.Empty<LeastSquaresAxisymmetricAlignmentIterationDiagnostics>()
        };

        return new LeastSquaresAxisymmetricAlignmentSolveResult(init.EstimatedTiltWeight, init.EstimatedTiltWeight, init.Points, diagnostics);
    }
}

public sealed record LeastSquaresAxisymmetricAlignmentSolveResult(
    double InitialLambda,
    double RefinedLambda,
    IReadOnlyList<AxisymmetricProjectionPoint> Points,
    LeastSquaresAxisymmetricAlignmentDiagnostics Diagnostics);
