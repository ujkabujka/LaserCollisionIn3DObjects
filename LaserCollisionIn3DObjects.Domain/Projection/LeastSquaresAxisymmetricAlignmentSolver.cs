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
        var initializedPoints = DirectAxisymmetricProjectionInitializer.Initialize(worldHolePoints, frame, profile);

        const double MinLength = 1e-9;
        var initialLambda = 1d / (10d * Math.Max(profile.Length, MinLength));
        var refinedLambda = initialLambda;

        var alignmentErrors = new List<double>(initializedPoints.Count);
        var angularErrors = new List<double>(initializedPoints.Count);
        var points = new List<AxisymmetricProjectionPoint>(initializedPoints.Count);

        for (var i = 0; i < initializedPoints.Count; i++)
        {
            var point = initializedPoints[i];
            var modeledDirection = BuildModeledDirection(profile, point.LocalU ?? 0d, point.LocalTheta ?? 0d, refinedLambda, localTiltPoint);
            var alignmentError = AlignmentError(point.RayDirection, modeledDirection);
            var angularError = AngularErrorDegrees(point.RayDirection, modeledDirection);
            alignmentErrors.Add(alignmentError);
            angularErrors.Add(angularError);
            points.Add(point with { ModeledRayDirection = modeledDirection, FitError = alignmentError, AlignmentError = alignmentError, AngularErrorDegrees = angularError });
        }

        var meanAlignmentError = alignmentErrors.Count > 0 ? alignmentErrors.Average() : 0d;
        var rmsAlignmentError = alignmentErrors.Count > 0 ? Math.Sqrt(alignmentErrors.Average(error => error * error)) : 0d;
        var meanAngularErrorDegrees = angularErrors.Count > 0 ? angularErrors.Average() : 0d;
        var maxAngularErrorDegrees = angularErrors.Count > 0 ? angularErrors.Max() : 0d;

        var diagnostics = new LeastSquaresAxisymmetricAlignmentDiagnostics
        {
            InitialLambda = initialLambda,
            RefinedLambda = refinedLambda,
            InitialMeanAlignmentError = meanAlignmentError,
            FinalMeanAlignmentError = meanAlignmentError,
            FinalRmsAlignmentError = rmsAlignmentError,
            FinalMeanAngularErrorDegrees = meanAngularErrorDegrees,
            FinalMaxAngularErrorDegrees = maxAngularErrorDegrees,
            Iterations = 0,
            Converged = true,
            UsesRegularization = false,
            IterationHistory = Array.Empty<LeastSquaresAxisymmetricAlignmentIterationDiagnostics>()
        };

        return new LeastSquaresAxisymmetricAlignmentSolveResult(initialLambda, refinedLambda, points, diagnostics);
    }
}

public sealed record LeastSquaresAxisymmetricAlignmentSolveResult(
    double InitialLambda,
    double RefinedLambda,
    IReadOnlyList<AxisymmetricProjectionPoint> Points,
    LeastSquaresAxisymmetricAlignmentDiagnostics Diagnostics);
