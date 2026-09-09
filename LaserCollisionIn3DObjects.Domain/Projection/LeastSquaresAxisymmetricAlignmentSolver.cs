using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class LeastSquaresAxisymmetricAlignmentSolver
{
    public LeastSquaresAxisymmetricAlignmentSolverSettings Settings { get; }
    private const double MinLength = 1e-9;
    private const double AngleAlignmentWeight = 0.5d;
    private const double HitDistanceWeight = 0.5d;
    private const double DegenerateDirectionPenalty = 1e6d;

    public LeastSquaresAxisymmetricAlignmentSolver(LeastSquaresAxisymmetricAlignmentSolverSettings? settings = null)
    {
        Settings = settings ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default;
    }

    public static double AlignmentError(Vector3D actual, Vector3D modeled)
    {
        var dot = Math.Clamp(Dot(Normalize(actual), Normalize(modeled)), -1d, 1d);
        return 1d - dot;
    }

    public static double AngularErrorDegrees(Vector3D actual, Vector3D modeled)
    {
        var dot = Math.Clamp(Dot(Normalize(actual), Normalize(modeled)), -1d, 1d);
        return Math.Acos(dot) * (180d / Math.PI);
    }

    public static double Softplus(double x) => x > 40d ? x : Math.Log(1d + Math.Exp(x));
    public static double InverseSoftplus(double y) => y > 40d ? y : Math.Log(Math.Exp(y) - 1d);
    public static double ToU(double a, double length) => length / (1d + Math.Exp(-a));
    public static Point3 ParameterizeSurface(IAxisymmetricSourceProfile profile, double u, double theta) => SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(profile, u, theta);
    public static Vector3D BuildModeledDirection(IAxisymmetricSourceProfile profile, double u, double theta, double lambda, Point3 localTiltPoint) => SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(profile, u, theta, lambda, localTiltPoint);

    public LeastSquaresAxisymmetricAlignmentSolveResult Solve(IReadOnlyList<Point3> localHolePoints, PointSourceFrameState frame, IAxisymmetricSourceProfile profile, Point3 localTiltPoint, IReadOnlyList<Point3> worldHolePoints, IProgress<ProjectionProgress>? progress = null)
    {
        var initialized = DirectAxisymmetricProjectionInitializer.Initialize(worldHolePoints, frame, profile);
        var n = initialized.Count;
        var length = Math.Max(profile.Length, MinLength);
        var u = initialized.Select(p => Math.Clamp(p.LocalU ?? 0d, 0d, profile.Length)).ToArray();
        var theta = initialized.Select(p => WrapAngle(p.LocalTheta ?? 0d)).ToArray();
        var lambda = 1d / (10d * length);
        var initialLambda = lambda;

        var current = Evaluate(localHolePoints, worldHolePoints, frame, profile, localTiltPoint, u, theta, lambda);
        var initialMean = current.MeanAlignment;
        var history = new List<LeastSquaresAxisymmetricAlignmentIterationDiagnostics>();
        var converged = false;
        var iterationsDone = 0;

        for (var iteration = 1; iteration <= Settings.MaxIterations; iteration++)
        {
            iterationsDone = iteration;
            var previousObjective = current.Objective;
            var epsU = Math.Max(length, MinLength) * Settings.FiniteDifferenceRelativeStep;
            var epsTheta = Settings.FiniteDifferenceRelativeStep;
            var epsLambda = Settings.FiniteDifferenceRelativeStep;

            var gradU = new double[n];
            var gradTheta = new double[n];

            for (var i = 0; i < n; i++)
            {
                var plusU = LocalObjective(i, localHolePoints, profile, localTiltPoint, u[i] + epsU, theta[i], lambda);
                var minusU = LocalObjective(i, localHolePoints, profile, localTiltPoint, u[i] - epsU, theta[i], lambda);
                gradU[i] = (plusU - minusU) / (2d * epsU);

                var plusT = LocalObjective(i, localHolePoints, profile, localTiltPoint, u[i], theta[i] + epsTheta, lambda);
                var minusT = LocalObjective(i, localHolePoints, profile, localTiltPoint, u[i], theta[i] - epsTheta, lambda);
                gradTheta[i] = (plusT - minusT) / (2d * epsTheta);
            }

            var plusLambda = Evaluate(localHolePoints, worldHolePoints, frame, profile, localTiltPoint, u, theta, Math.Max(0d, lambda + epsLambda)).Objective;
            var minusLambda = Evaluate(localHolePoints, worldHolePoints, frame, profile, localTiltPoint, u, theta, Math.Max(0d, lambda - epsLambda)).Objective;
            var gradLambda = (plusLambda - minusLambda) / (2d * epsLambda);

            var improved = false;
            var pointStep = Settings.PointStepScale * length;
            var thetaStep = Settings.ThetaStepScale;
            var lambdaStep = Settings.LambdaStepScale * Math.Max(lambda, MinLength);
            EvaluationResult? acceptedCandidate = null;
            double[]? acceptedU = null;
            double[]? acceptedTheta = null;
            var acceptedLambda = lambda;

            for (var bt = 0; bt < Settings.MaxBacktrackingAttempts; bt++)
            {
                var nextU = new double[n];
                var nextTheta = new double[n];
                for (var i = 0; i < n; i++)
                {
                    nextU[i] = Math.Clamp(u[i] - (pointStep * gradU[i]), 0d, profile.Length);
                    nextTheta[i] = WrapAngle(theta[i] - (thetaStep * gradTheta[i]));
                }

                var nextLambda = Math.Max(0d, lambda - (lambdaStep * gradLambda));
                var candidate = Evaluate(localHolePoints, worldHolePoints, frame, profile, localTiltPoint, nextU, nextTheta, nextLambda);
                if (double.IsFinite(candidate.Objective) && candidate.Objective < current.Objective)
                {
                    acceptedU = nextU;
                    acceptedTheta = nextTheta;
                    acceptedLambda = nextLambda;
                    acceptedCandidate = candidate;
                    improved = true;
                    break;
                }

                pointStep *= Settings.BacktrackingFactor;
                thetaStep *= Settings.BacktrackingFactor;
                lambdaStep *= Settings.BacktrackingFactor;

            }

            if (improved)
            {
                u = acceptedU!;
                theta = acceptedTheta!;
                lambda = acceptedLambda;
                current = acceptedCandidate!;
            }

            history.Add(new LeastSquaresAxisymmetricAlignmentIterationDiagnostics(iteration, current.Objective, lambda, current.MeanAlignment, current.RmsAlignment, current.MeanAngular, current.MaxAngular, improved));
            progress?.Report(new ProjectionProgress((int)Math.Round(100d * iteration / Math.Max(Settings.MaxIterations, 1)), $"Least-squares iteration {iteration}/{Settings.MaxIterations}: objective={current.Objective:F6}, mean angular error={current.MeanAngular:F3} deg, lambda={lambda:F6}"));

            var improvement = previousObjective - current.Objective;
            if (!improved || improvement <= Settings.ConvergenceTolerance)
            {
                // A failed monotonic line search is treated as convergence at a stationary/stalled point.
                converged = true;
                progress?.Report(new ProjectionProgress(100d, $"Least-squares converged at iteration {iteration} with objective {current.Objective:F6}."));
                break;
            }
        }

        var diagnostics = new LeastSquaresAxisymmetricAlignmentDiagnostics
        {
            InitialLambda = initialLambda,
            RefinedLambda = lambda,
            InitialMeanAlignmentError = initialMean,
            FinalMeanAlignmentError = current.MeanAlignment,
            FinalRmsAlignmentError = current.RmsAlignment,
            FinalMeanAngularErrorDegrees = current.MeanAngular,
            FinalMaxAngularErrorDegrees = current.MaxAngular,
            MaxAngularErrorHoleIndex = current.MaxAngularIndex,
            Iterations = iterationsDone,
            Converged = converged,
            UsesRegularization = false,
            IterationHistory = history
        };

        return new LeastSquaresAxisymmetricAlignmentSolveResult(initialLambda, lambda, current.Points, diagnostics);
    }

    private static double LocalObjective(int index, IReadOnlyList<Point3> localHolePoints, IAxisymmetricSourceProfile profile, Point3 localTiltPoint, double u, double theta, double lambda)
    {
        var sourceLocal = ParameterizeSurface(profile, u, theta);
        var modeledDirection = Normalize(BuildModeledDirection(profile, u, theta, lambda, localTiltPoint));
        var lengthScale = Math.Max(profile.Length, MinLength);
        var holeLocal = localHolePoints[index];

        var angleTerm = AngleAlignmentResidualSquared(holeLocal, sourceLocal, modeledDirection);
        var hitTerm = HitDistanceResidualSquared(holeLocal, sourceLocal, modeledDirection, lengthScale);
        return (AngleAlignmentWeight * angleTerm) + (HitDistanceWeight * hitTerm);
    }

    private static double AngleAlignmentResidualSquared(Point3 holeLocal, Point3 sourceLocal, Vector3D modeledDirection)
    {
        var sourceToHole = new Vector3D(holeLocal.X - sourceLocal.X, holeLocal.Y - sourceLocal.Y, holeLocal.Z - sourceLocal.Z);
        var sourceToHoleMagnitude = Math.Sqrt((sourceToHole.X * sourceToHole.X) + (sourceToHole.Y * sourceToHole.Y) + (sourceToHole.Z * sourceToHole.Z));
        if (sourceToHoleMagnitude <= MinLength)
        {
            return DegenerateDirectionPenalty;
        }

        var actualDirection = new Vector3D(sourceToHole.X / sourceToHoleMagnitude, sourceToHole.Y / sourceToHoleMagnitude, sourceToHole.Z / sourceToHoleMagnitude);
        var alignmentResidual = 1d - Math.Clamp(Dot(actualDirection, modeledDirection), -1d, 1d);
        return alignmentResidual * alignmentResidual;
    }

    private static double HitDistanceResidualSquared(Point3 holeLocal, Point3 sourceLocal, Vector3D modeledDirection, double lengthScale)
    {
        var sourceToHole = new Vector3D(holeLocal.X - sourceLocal.X, holeLocal.Y - sourceLocal.Y, holeLocal.Z - sourceLocal.Z);
        var t = Math.Max(0d, Dot(sourceToHole, modeledDirection));
        var closest = new Point3(
            sourceLocal.X + (t * modeledDirection.X),
            sourceLocal.Y + (t * modeledDirection.Y),
            sourceLocal.Z + (t * modeledDirection.Z));
        var error = new Vector3D(holeLocal.X - closest.X, holeLocal.Y - closest.Y, holeLocal.Z - closest.Z);
        var squaredDistance = (error.X * error.X) + (error.Y * error.Y) + (error.Z * error.Z);
        var normalizedScale = Math.Max(lengthScale, MinLength);
        return squaredDistance / (normalizedScale * normalizedScale);
    }

    private static EvaluationResult Evaluate(IReadOnlyList<Point3> localHolePoints, IReadOnlyList<Point3> worldHolePoints, PointSourceFrameState frame, IAxisymmetricSourceProfile profile, Point3 localTiltPoint, IReadOnlyList<double> u, IReadOnlyList<double> theta, double lambda)
    {
        var points = new List<AxisymmetricProjectionPoint>(u.Count);
        var align = new List<double>(u.Count);
        var ang = new List<double>(u.Count);
        var objective = 0d;

        for (var i = 0; i < u.Count; i++)
        {
            var uu = Math.Clamp(u[i], 0d, profile.Length);
            var tt = WrapAngle(theta[i]);
            var sourceLocal = ParameterizeSurface(profile, uu, tt);
            var sourceWorld = PointSourceFrameTransforms.LocalToWorld(sourceLocal, frame);
            var sourceToHoleLocal = new Vector3D(localHolePoints[i].X - sourceLocal.X, localHolePoints[i].Y - sourceLocal.Y, localHolePoints[i].Z - sourceLocal.Z);
            var actualLocal = Normalize(sourceToHoleLocal);
            var modeledLocal = Normalize(BuildModeledDirection(profile, uu, tt, lambda, localTiltPoint));
            var actualWorld = Normalize(new Vector3D(worldHolePoints[i].X - sourceWorld.X, worldHolePoints[i].Y - sourceWorld.Y, worldHolePoints[i].Z - sourceWorld.Z));
            var modeledWorld = Normalize(PointSourceFrameTransforms.LocalDirectionToWorld(modeledLocal, frame));
            var alignment = AlignmentError(actualLocal, modeledLocal);
            var angular = AngularErrorDegrees(actualLocal, modeledLocal);
            var angleTerm = AngleAlignmentResidualSquared(localHolePoints[i], sourceLocal, modeledLocal);
            var hitTerm = HitDistanceResidualSquared(localHolePoints[i], sourceLocal, modeledLocal, Math.Max(profile.Length, MinLength));
            var localObjective = (AngleAlignmentWeight * angleTerm) + (HitDistanceWeight * hitTerm);
            objective += localObjective;
            align.Add(alignment);
            ang.Add(angular);

            points.Add(new AxisymmetricProjectionPoint(worldHolePoints[i], sourceWorld, actualWorld, sourceWorld)
            {
                ModeledRayDirection = modeledWorld,
                LocalU = uu,
                LocalTheta = tt,
                UnwrappedU = uu,
                UnwrappedV = profile.RadiusAt((float)uu) * tt,
                AlignmentError = alignment,
                AngularErrorDegrees = angular,
                FitError = localObjective,
            });
        }

        var meanAlign = align.Count == 0 ? 0d : align.Average();
        var rmsAlign = align.Count == 0 ? 0d : Math.Sqrt(align.Average(v => v * v));
        var meanAng = ang.Count == 0 ? 0d : ang.Average();
        var maxAng = ang.Count == 0 ? 0d : ang.Max();
        var maxIndex = ang.Count == 0 ? (int?)null : ang.IndexOf(maxAng);
        return new EvaluationResult(objective, meanAlign, rmsAlign, meanAng, maxAng, maxIndex, points);
    }

    private static double Dot(Vector3D a, Vector3D b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
    private static Vector3D Normalize(Vector3D value) { var m = Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z); return m > 0d ? new Vector3D(value.X / m, value.Y / m, value.Z / m) : new Vector3D(0, 0, 0); }
    private static double WrapAngle(double theta) { var twoPi = 2d * Math.PI; var wrapped = theta % twoPi; return wrapped < 0d ? wrapped + twoPi : wrapped; }

    private sealed record EvaluationResult(double Objective, double MeanAlignment, double RmsAlignment, double MeanAngular, double MaxAngular, int? MaxAngularIndex, IReadOnlyList<AxisymmetricProjectionPoint> Points);
}

public sealed record LeastSquaresAxisymmetricAlignmentSolveResult(double InitialLambda, double RefinedLambda, IReadOnlyList<AxisymmetricProjectionPoint> Points, LeastSquaresAxisymmetricAlignmentDiagnostics Diagnostics);
