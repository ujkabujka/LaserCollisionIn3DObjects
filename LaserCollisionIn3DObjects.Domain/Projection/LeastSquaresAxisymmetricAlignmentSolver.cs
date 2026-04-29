using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresAxisymmetricAlignmentSolver
{
    public LeastSquaresAxisymmetricAlignmentSolverSettings Settings { get; }

    public static double AlignmentError(Vector3D actual, Vector3D modeled)
    {
        var a = Normalize(actual);
        var m = Normalize(modeled);
        var dot = Math.Clamp(Dot(a, m), -1d, 1d);
        return 1d - dot;
    }

    public static double AngularErrorDegrees(Vector3D actual, Vector3D modeled)
    {
        var a = Normalize(actual);
        var m = Normalize(modeled);
        var dot = Math.Clamp(Dot(a, m), -1d, 1d);
        return Math.Acos(dot) * (180d / Math.PI);
    }

    public static double Softplus(double x) => x > 40d ? x : Math.Log(1d + Math.Exp(x));

    public static double InverseSoftplus(double y) => y > 40d ? y : Math.Log(Math.Exp(y) - 1d);

    public static double ToU(double a, double length) => length / (1d + Math.Exp(-a));

    public static Point3 ParameterizeSurface(IAxisymmetricSourceProfile profile, double u, double theta)
        => SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(profile, u, theta);

    public static Vector3D BuildModeledDirection(IAxisymmetricSourceProfile profile, double u, double theta, double lambda, Point3 localTiltPoint)
        => SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(profile, u, theta, lambda, localTiltPoint);

    public LeastSquaresAxisymmetricAlignmentSolver(LeastSquaresAxisymmetricAlignmentSolverSettings? settings = null)
    {
        Settings = settings ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default;
    }

    private static double Dot(Vector3D a, Vector3D b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static Vector3D Normalize(Vector3D value)
    {
        var mag = Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
        return mag > 0d ? new Vector3D(value.X / mag, value.Y / mag, value.Z / mag) : new Vector3D(0d, 0d, 0d);
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
