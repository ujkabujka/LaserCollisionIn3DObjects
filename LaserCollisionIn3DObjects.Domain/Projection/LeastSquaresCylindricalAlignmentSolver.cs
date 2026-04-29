using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresCylindricalAlignmentSolver
{
    private const double TwoPi = Math.PI * 2d;
    private const double Epsilon = 1e-9;

    public LeastSquaresCylindricalAlignmentSolverSettings Settings { get; }

    public LeastSquaresCylindricalAlignmentSolver(LeastSquaresCylindricalAlignmentSolverSettings? settings = null)
    {
        Settings = settings ?? LeastSquaresCylindricalAlignmentSolverSettings.Default;
    }

    public LeastSquaresCylindricalAlignmentSolveResult Solve(
        IReadOnlyList<Point3> localHolePoints,
        PointSourceFrameState frame,
        double radius,
        double length,
        Point3 localTiltPoint,
        IReadOnlyList<Point3> worldHolePoints,
        IProgress<ProjectionProgress>? progress = null)
    {
        progress?.Report(new ProjectionProgress(0d, "Initializing least-squares cylindrical alignment..."));

        var initSolver = new SelfCalibratingAxisymmetricProjectionSolver(new SelfCalibratingAxisymmetricProjectionSolverSettings
        {
            KappaCandidates = SelfCalibratingAxisymmetricProjectionSolverSettings.Default.KappaCandidates,
            AxialSamples = SelfCalibratingAxisymmetricProjectionSolverSettings.Default.AxialSamples,
            AngularSamples = SelfCalibratingAxisymmetricProjectionSolverSettings.Default.AngularSamples,
            RefinementIterations = SelfCalibratingAxisymmetricProjectionSolverSettings.Default.RefinementIterations,
            RegularityWeight = 0d,
        });

        progress?.Report(new ProjectionProgress(5d, "Running initial self-calibrating estimate..."));
        var init = initSolver.Solve(localHolePoints, frame, radius, length, localTiltPoint, worldHolePoints, progress: null);

        var scale = Math.Max(Math.Max(radius, length), Math.Max(Norm(localTiltPoint), Epsilon));
        var beta = InverseSoftplus(Math.Max(0d, init.EstimatedTiltWeight * scale));
        var a = new double[localHolePoints.Count];
        var theta = new double[localHolePoints.Count];

        for (var i = 0; i < localHolePoints.Count; i++)
        {
            var p = init.Points[i];
            var u0 = p.LocalU ?? 0d;
            var q = Math.Clamp(u0 / Math.Max(length, Epsilon), Epsilon, 1d - Epsilon);
            a[i] = Math.Log(q / (1d - q));
            theta[i] = p.LocalTheta ?? 0d;
        }

        progress?.Report(new ProjectionProgress(20d, "Starting nonlinear alignment refinement..."));

        var iterationHistory = new List<LeastSquaresCylindricalAlignmentIterationDiagnostics>();
        var initialMetrics = ComputeMetrics(localHolePoints, a, theta, beta, radius, length, localTiltPoint, scale);
        var previousCost = initialMetrics.TotalAlignmentError;
        var converged = false;
        var iterationsCompleted = 0;

        for (var iter = 1; iter <= Settings.MaxIterations; iter++)
        {
            for (var i = 0; i < localHolePoints.Count; i++)
            {
                RefinePoint(localHolePoints[i], ref a[i], ref theta[i], beta, radius, length, localTiltPoint, scale);
            }

            beta = RefineBeta(localHolePoints, a, theta, beta, radius, length, localTiltPoint, scale);

            var metrics = ComputeMetrics(localHolePoints, a, theta, beta, radius, length, localTiltPoint, scale);
            var lambda = Softplus(beta) / scale;
            iterationHistory.Add(new LeastSquaresCylindricalAlignmentIterationDiagnostics(iter, lambda, metrics.MeanAlignmentError, metrics.MeanAngularErrorDegrees));
            iterationsCompleted = iter;

            progress?.Report(new ProjectionProgress(20d + ((80d * iter) / Settings.MaxIterations),
                $"Iteration {iter}/{Settings.MaxIterations}: mean angular error {metrics.MeanAngularErrorDegrees:F3}°; lambda {lambda:F6}"));

            if (Math.Abs(previousCost - metrics.TotalAlignmentError) <= Settings.ConvergenceTolerance)
            {
                converged = true;
                break;
            }

            previousCost = metrics.TotalAlignmentError;
        }

        var refinedMetrics = ComputeMetrics(localHolePoints, a, theta, beta, radius, length, localTiltPoint, scale);
        var refinedLambda = Softplus(beta) / scale;
        var points = BuildPoints(localHolePoints, worldHolePoints, frame, a, theta, refinedLambda, radius, length, localTiltPoint);

        progress?.Report(new ProjectionProgress(100d, "Least-squares cylindrical alignment completed."));

        return new LeastSquaresCylindricalAlignmentSolveResult(
            init.EstimatedTiltWeight,
            refinedLambda,
            points,
            new LeastSquaresCylindricalAlignmentDiagnostics
            {
                InitialLambda = init.EstimatedTiltWeight,
                RefinedLambda = refinedLambda,
                InitialMeanAlignmentError = initialMetrics.MeanAlignmentError,
                FinalMeanAlignmentError = refinedMetrics.MeanAlignmentError,
                FinalRmsAlignmentError = refinedMetrics.RmsAlignmentError,
                FinalMeanAngularErrorDegrees = refinedMetrics.MeanAngularErrorDegrees,
                FinalMaxAngularErrorDegrees = refinedMetrics.MaxAngularErrorDegrees,
                MaxAngularErrorHoleIndex = refinedMetrics.MaxAngularErrorHoleIndex,
                Iterations = iterationsCompleted,
                Converged = converged,
                UsesRegularization = false,
                IterationHistory = iterationHistory,
            });
    }

    private List<CylindricalProjectionPoint> BuildPoints(
        IReadOnlyList<Point3> localHolePoints,
        IReadOnlyList<Point3> worldHolePoints,
        PointSourceFrameState frame,
        IReadOnlyList<double> a,
        IReadOnlyList<double> theta,
        double lambda,
        double radius,
        double length,
        Point3 localTiltPoint)
    {
        var points = new List<CylindricalProjectionPoint>(localHolePoints.Count);

        for (var i = 0; i < localHolePoints.Count; i++)
        {
            var u = ToU(a[i], length);
            var wrappedTheta = WrapTheta(theta[i]);
            var sourceLocal = ParameterizeSurface(u, wrappedTheta, radius, length);
            var modeledLocal = BuildModeledDirection(u, wrappedTheta, lambda, radius, localTiltPoint);
            var sourceWorld = ToWorld(sourceLocal, frame);
            var modeledWorld = LocalDirectionToWorld(modeledLocal, frame);
            var actualWorld = BuildNormalizedDirection(sourceWorld, worldHolePoints[i], $"Hole point at index {i} coincides with reconstructed source point.");

            var alignmentError = VectorError(actualWorld, modeledWorld);
            var angularError = AngularErrorDegrees(actualWorld, modeledWorld);

            points.Add(new CylindricalProjectionPoint(
                worldHolePoints[i],
                sourceWorld,
                actualWorld,
                sourceWorld)
            {
                ModeledRayDirection = modeledWorld,
                LocalU = u,
                LocalTheta = wrappedTheta,
                UnwrappedU = u,
                UnwrappedV = radius * wrappedTheta,
                AlignmentError = alignmentError,
                AngularErrorDegrees = angularError,
                FitError = alignmentError,
            });
        }

        return points;
    }

    private void RefinePoint(
        Point3 holeLocal,
        ref double a,
        ref double theta,
        double beta,
        double radius,
        double length,
        Point3 localTiltPoint,
        double scale)
    {
        var stepA = Settings.PointStepScale;
        var stepTheta = Settings.PointStepScale;

        for (var iter = 0; iter < Settings.PointRefinementIterations; iter++)
        {
            var current = SinglePointCost(holeLocal, a, theta, beta, radius, length, localTiltPoint, scale);
            var improved = false;

            foreach (var candidateA in new[] { a - stepA, a, a + stepA })
            {
                foreach (var candidateTheta in new[] { theta - stepTheta, theta, theta + stepTheta })
                {
                    var candidateCost = SinglePointCost(holeLocal, candidateA, candidateTheta, beta, radius, length, localTiltPoint, scale);
                    if (candidateCost + 1e-12 < current)
                    {
                        a = candidateA;
                        theta = candidateTheta;
                        current = candidateCost;
                        improved = true;
                    }
                }
            }

            if (!improved)
            {
                stepA *= 0.5d;
                stepTheta *= 0.5d;
            }
        }
    }

    private double RefineBeta(
        IReadOnlyList<Point3> holes,
        IReadOnlyList<double> a,
        IReadOnlyList<double> theta,
        double beta,
        double radius,
        double length,
        Point3 localTiltPoint,
        double scale)
    {
        var step = Settings.LambdaStepScale;
        var current = TotalCost(holes, a, theta, beta, radius, length, localTiltPoint, scale);

        for (var iter = 0; iter < Settings.LambdaRefinementIterations; iter++)
        {
            var candidateMinus = beta - step;
            var candidatePlus = beta + step;
            var minusCost = TotalCost(holes, a, theta, candidateMinus, radius, length, localTiltPoint, scale);
            var plusCost = TotalCost(holes, a, theta, candidatePlus, radius, length, localTiltPoint, scale);

            if (minusCost < current || plusCost < current)
            {
                if (minusCost <= plusCost)
                {
                    beta = candidateMinus;
                    current = minusCost;
                }
                else
                {
                    beta = candidatePlus;
                    current = plusCost;
                }
            }
            else
            {
                step *= 0.5d;
            }
        }

        return beta;
    }

    private Metrics ComputeMetrics(
        IReadOnlyList<Point3> holes,
        IReadOnlyList<double> a,
        IReadOnlyList<double> theta,
        double beta,
        double radius,
        double length,
        Point3 localTiltPoint,
        double scale)
    {
        var lambda = Softplus(beta) / scale;
        var total = 0d;
        var sum = 0d;
        var sumSq = 0d;
        var angleSum = 0d;
        var maxAngle = double.MinValue;
        var maxAngleIndex = -1;

        for (var i = 0; i < holes.Count; i++)
        {
            var u = ToU(a[i], length);
            var t = theta[i];
            var source = ParameterizeSurface(u, t, radius, length);
            var actual = Normalize(new Vector3((float)(holes[i].X - source.X), (float)(holes[i].Y - source.Y), (float)(holes[i].Z - source.Z)));
            var modeled = BuildModeledDirectionVec(u, t, lambda, radius, localTiltPoint);
            var dx = actual.X - modeled.X;
            var dy = actual.Y - modeled.Y;
            var dz = actual.Z - modeled.Z;
            var align = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            var dot = Math.Clamp((actual.X * modeled.X) + (actual.Y * modeled.Y) + (actual.Z * modeled.Z), -1d, 1d);
            var angle = Math.Acos(dot) * 180d / Math.PI;

            total += (dx * dx) + (dy * dy) + (dz * dz);
            sum += align;
            sumSq += align * align;
            angleSum += angle;
            if (angle > maxAngle)
            {
                maxAngle = angle;
                maxAngleIndex = i;
            }
        }

        var count = Math.Max(holes.Count, 1);
        return new Metrics(total, sum / count, Math.Sqrt(sumSq / count), angleSum / count, maxAngle, maxAngleIndex >= 0 ? maxAngleIndex : null);
    }

    private static double TotalCost(
        IReadOnlyList<Point3> holes,
        IReadOnlyList<double> a,
        IReadOnlyList<double> theta,
        double beta,
        double radius,
        double length,
        Point3 localTiltPoint,
        double scale)
    {
        var lambda = Softplus(beta) / scale;
        var total = 0d;

        for (var i = 0; i < holes.Count; i++)
        {
            var u = ToU(a[i], length);
            total += SinglePointCost(holes[i], u, theta[i], lambda, radius, length, localTiltPoint);
        }

        return total;
    }

    private static double SinglePointCost(
        Point3 holeLocal,
        double a,
        double theta,
        double beta,
        double radius,
        double length,
        Point3 localTiltPoint,
        double scale)
    {
        var u = ToU(a, length);
        var lambda = Softplus(beta) / scale;
        return SinglePointCost(holeLocal, u, theta, lambda, radius, length, localTiltPoint);
    }

    private static double SinglePointCost(Point3 holeLocal, double u, double theta, double lambda, double radius, double length, Point3 localTiltPoint)
    {
        var source = ParameterizeSurface(u, theta, radius, length);
        var actual = Normalize(new Vector3((float)(holeLocal.X - source.X), (float)(holeLocal.Y - source.Y), (float)(holeLocal.Z - source.Z)));
        var modeled = BuildModeledDirectionVec(u, theta, lambda, radius, localTiltPoint);
        var dx = actual.X - modeled.X;
        var dy = actual.Y - modeled.Y;
        var dz = actual.Z - modeled.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    public static Point3 ParameterizeSurface(double u, double theta, double radius, double length)
    {
        var clampedU = Math.Clamp(u, 0d, length);
        var wrappedTheta = WrapTheta(theta);
        return new Point3(clampedU, radius * Math.Cos(wrappedTheta), radius * Math.Sin(wrappedTheta));
    }

    public static Vector3D BuildModeledDirection(double u, double theta, double lambda, double radius, Point3 localTiltPoint)
    {
        var vec = BuildModeledDirectionVec(u, theta, lambda, radius, localTiltPoint);
        return new Vector3D(vec.X, vec.Y, vec.Z);
    }

    public static double AlignmentError(Vector3D actual, Vector3D modeled)
    {
        return VectorError(actual, modeled);
    }

    public static double AngularErrorDegrees(Vector3D actual, Vector3D modeled)
    {
        var dot = Math.Clamp((actual.X * modeled.X) + (actual.Y * modeled.Y) + (actual.Z * modeled.Z), -1d, 1d);
        return Math.Acos(dot) * 180d / Math.PI;
    }

    public static double ToU(double a, double length) => length * Sigmoid(a);

    public static double Sigmoid(double x)
    {
        if (x >= 0d)
        {
            var z = Math.Exp(-x);
            return 1d / (1d + z);
        }

        var y = Math.Exp(x);
        return y / (1d + y);
    }

    public static double Softplus(double x)
    {
        if (x > 40d)
        {
            return x;
        }

        if (x < -40d)
        {
            return Math.Exp(x);
        }

        return Math.Log(1d + Math.Exp(x));
    }

    public static double InverseSoftplus(double y)
    {
        var clamped = Math.Max(y, Epsilon);
        if (clamped > 40d)
        {
            return clamped;
        }

        return clamped + Math.Log(1d - Math.Exp(-clamped));
    }

    public static double WrapTheta(double theta)
    {
        var wrapped = theta % TwoPi;
        if (wrapped < 0d)
        {
            wrapped += TwoPi;
        }

        return wrapped;
    }

    private static Vector3 BuildModeledDirectionVec(double u, double theta, double lambda, double radius, Point3 localTiltPoint)
    {
        var wrappedTheta = WrapTheta(theta);
        var source = new Point3(u, radius * Math.Cos(wrappedTheta), radius * Math.Sin(wrappedTheta));
        var raw = new Vector3(
            (float)(lambda * (source.X - localTiltPoint.X)),
            (float)(Math.Cos(wrappedTheta) + (lambda * (source.Y - localTiltPoint.Y))),
            (float)(Math.Sin(wrappedTheta) + (lambda * (source.Z - localTiltPoint.Z))));

        return Normalize(raw);
    }

    private static Point3 ToWorld(Point3 localPoint, PointSourceFrameState frame)
    {
        return new Point3(
            frame.Origin.X + (localPoint.X * frame.AxisX.X) + (localPoint.Y * frame.AxisY.X) + (localPoint.Z * frame.AxisZ.X),
            frame.Origin.Y + (localPoint.X * frame.AxisX.Y) + (localPoint.Y * frame.AxisY.Y) + (localPoint.Z * frame.AxisZ.Y),
            frame.Origin.Z + (localPoint.X * frame.AxisX.Z) + (localPoint.Y * frame.AxisY.Z) + (localPoint.Z * frame.AxisZ.Z));
    }

    private static Vector3D LocalDirectionToWorld(Vector3D localDirection, PointSourceFrameState frame)
    {
        var worldX = (localDirection.X * frame.AxisX.X) + (localDirection.Y * frame.AxisY.X) + (localDirection.Z * frame.AxisZ.X);
        var worldY = (localDirection.X * frame.AxisX.Y) + (localDirection.Y * frame.AxisY.Y) + (localDirection.Z * frame.AxisZ.Y);
        var worldZ = (localDirection.X * frame.AxisX.Z) + (localDirection.Y * frame.AxisY.Z) + (localDirection.Z * frame.AxisZ.Z);

        var v = Normalize(new Vector3((float)worldX, (float)worldY, (float)worldZ));
        return new Vector3D(v.X, v.Y, v.Z);
    }

    private static Vector3D BuildNormalizedDirection(Point3 origin, Point3 target, string errorMessage)
    {
        var directionVector = new Vector3(
            (float)(target.X - origin.X),
            (float)(target.Y - origin.Y),
            (float)(target.Z - origin.Z));

        if (directionVector.LengthSquared() <= 0f)
        {
            throw new ArgumentException(errorMessage);
        }

        var direction = Vector3.Normalize(directionVector);
        return new Vector3D(direction.X, direction.Y, direction.Z);
    }

    private static double VectorError(Vector3D a, Vector3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static Vector3 Normalize(Vector3 vector)
    {
        if (vector.LengthSquared() <= 0f)
        {
            throw new InvalidOperationException("Direction collapsed to zero vector.");
        }

        return Vector3.Normalize(vector);
    }

    private static double Norm(Point3 p)
    {
        return Math.Sqrt((p.X * p.X) + (p.Y * p.Y) + (p.Z * p.Z));
    }

    private sealed record Metrics(
        double TotalAlignmentError,
        double MeanAlignmentError,
        double RmsAlignmentError,
        double MeanAngularErrorDegrees,
        double MaxAngularErrorDegrees,
        int? MaxAngularErrorHoleIndex);
}

public sealed record LeastSquaresCylindricalAlignmentSolveResult(
    double InitialLambda,
    double RefinedLambda,
    IReadOnlyList<CylindricalProjectionPoint> Points,
    LeastSquaresCylindricalAlignmentDiagnostics Diagnostics);
