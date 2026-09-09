using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class SelfCalibratingAxisymmetricProjectionSolver
{
    private const double TwoPi = Math.PI * 2d;
    private const double Epsilon = 1e-9;

    public SelfCalibratingAxisymmetricProjectionSolverSettings Settings { get; }

    public SelfCalibratingAxisymmetricProjectionSolver(SelfCalibratingAxisymmetricProjectionSolverSettings? settings = null)
    {
        Settings = settings ?? SelfCalibratingAxisymmetricProjectionSolverSettings.Default;
    }

    public SelfCalibratingSolveResult Solve(
        IReadOnlyList<Point3> localHolePoints,
        PointSourceFrameState frame,
        IAxisymmetricSourceProfile profile,
        Point3 localTiltPoint,
        IReadOnlyList<Point3> worldHolePoints,
        IProgress<ProjectionProgress>? progress = null)
    {
        var scale = Math.Max(profile.Length, Math.Max(Math.Sqrt((localTiltPoint.X * localTiltPoint.X) + (localTiltPoint.Y * localTiltPoint.Y) + (localTiltPoint.Z * localTiltPoint.Z)), Epsilon));
        var candidateDiagnostics = new List<SelfCalibratingAxisymmetricCandidateDiagnostics>(Settings.KappaCandidates.Count);

        CandidateResult? best = null;

        for (var c = 0; c < Settings.KappaCandidates.Count; c++)
        {
            var lambda = Settings.KappaCandidates[c] / scale;
            progress?.Report(new ProjectionProgress((100d * c) / Math.Max(Settings.KappaCandidates.Count, 1), $"Testing tilt candidate {c + 1}/{Settings.KappaCandidates.Count}..."));

            var points = new List<AxisymmetricProjectionPoint>(localHolePoints.Count);
            var fitErrorSum = 0d;

            for (var i = 0; i < localHolePoints.Count; i++)
            {
                if (i % 10 == 0)
                {
                    var coarse = (double)c / Settings.KappaCandidates.Count;
                    var fine = (double)i / Math.Max(localHolePoints.Count, 1);
                    progress?.Report(new ProjectionProgress((coarse + (fine / Settings.KappaCandidates.Count)) * 100d, $"Reconstructing hole {i + 1}/{localHolePoints.Count}..."));
                }

                var solved = SolveSingleHole(localHolePoints[i], lambda, profile, localTiltPoint);
                fitErrorSum += solved.FitError;

                var sourceWorld = PointSourceFrameTransforms.LocalToWorld(solved.SourceLocal, frame);
                var modeledWorld = PointSourceFrameTransforms.LocalDirectionToWorld(solved.ModeledLocalDirection, frame);
                var actualWorld = BuildNormalizedDirection(sourceWorld, worldHolePoints[i], $"Hole point at index {i} coincides with reconstructed source point.");

                points.Add(new AxisymmetricProjectionPoint(
                    worldHolePoints[i],
                    sourceWorld,
                    actualWorld,
                    sourceWorld)
                {
                    ModeledRayDirection = modeledWorld,
                    LocalU = solved.U,
                    LocalTheta = solved.Theta,
                    UnwrappedU = solved.U,
                    UnwrappedV = profile.RadiusAt((float)solved.U) * solved.Theta,
                    FitError = solved.FitError,
                });
            }

            var meanFit = fitErrorSum / Math.Max(localHolePoints.Count, 1);
            var regularity = ComputeRegularity(points);
            var score = meanFit + (Settings.RegularityWeight * regularity);
            candidateDiagnostics.Add(new SelfCalibratingAxisymmetricCandidateDiagnostics(lambda, meanFit, regularity, score));

            if (best is null || score < best.Score)
            {
                best = new CandidateResult(lambda, score, points);
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException("Self-calibrating axisymmetric solver failed to evaluate candidates.");
        }

        progress?.Report(new ProjectionProgress(100d, "Projection complete."));

        return new SelfCalibratingSolveResult(
            best.Lambda,
            best.Points,
            new SelfCalibratingAxisymmetricProjectionDiagnostics
            {
                CandidateScores = candidateDiagnostics,
                RegularityWeight = Settings.RegularityWeight,
            });
    }

    private SolvedPoint SolveSingleHole(Point3 holeLocal, double lambda, IAxisymmetricSourceProfile profile, Point3 localTiltPoint)
    {
        var bestU = 0d;
        var bestTheta = 0d;
        var bestErrorSq = double.MaxValue;

        for (var iu = 0; iu < Settings.AxialSamples; iu++)
        {
            var u = (profile.Length * iu) / (Settings.AxialSamples - 1d);
            for (var it = 0; it < Settings.AngularSamples; it++)
            {
                var theta = (TwoPi * it) / Settings.AngularSamples;
                var errorSq = PointToRayError(holeLocal, u, theta, lambda, profile, localTiltPoint);
                if (errorSq < bestErrorSq)
                {
                    bestErrorSq = errorSq;
                    bestU = u;
                    bestTheta = theta;
                }
            }
        }

        var uStep = profile.Length / Math.Max(1d, Settings.AxialSamples - 1d);
        var tStep = TwoPi / Math.Max(1d, Settings.AngularSamples);

        for (var iteration = 0; iteration < Settings.RefinementIterations; iteration++)
        {
            var candidateImproved = false;
            foreach (var uCandidate in new[] { bestU - uStep, bestU, bestU + uStep })
            {
                var clampedU = Math.Clamp(uCandidate, 0d, profile.Length);
                foreach (var tCandidate in new[] { bestTheta - tStep, bestTheta, bestTheta + tStep })
                {
                    var wrappedTheta = WrapTheta(tCandidate);
                    var errorSq = PointToRayError(holeLocal, clampedU, wrappedTheta, lambda, profile, localTiltPoint);
                    if (errorSq < bestErrorSq)
                    {
                        bestErrorSq = errorSq;
                        bestU = clampedU;
                        bestTheta = wrappedTheta;
                        candidateImproved = true;
                    }
                }
            }

            if (!candidateImproved)
            {
                uStep *= 0.5d;
                tStep *= 0.5d;
            }
        }

        var sourceLocal = ParameterizeSurface(profile, bestU, bestTheta);
        var modeledLocalDirection = BuildModeledDirection(profile, bestU, bestTheta, lambda, localTiltPoint);

        return new SolvedPoint(bestU, bestTheta, sourceLocal, modeledLocalDirection, Math.Sqrt(bestErrorSq));
    }

    public static Point3 ParameterizeSurface(IAxisymmetricSourceProfile profile, double u, double theta)
    {
        var clampedU = Math.Clamp(u, 0d, profile.Length);
        var wrappedTheta = WrapTheta(theta);
        var p = profile.EvaluateSurfacePoint((float)clampedU, (float)wrappedTheta);
        return new Point3(p.X, p.Y, p.Z);
    }

    public static Vector3D BuildModeledDirection(IAxisymmetricSourceProfile profile, double u, double theta, double lambda, Point3 localTiltPoint)
    {
        var wrappedTheta = WrapTheta(theta);
        var surface = ParameterizeSurface(profile, u, wrappedTheta);
        var baseDirection = profile.EvaluateBaseDirection((float)u, (float)wrappedTheta);
        var raw = new Vector3(
            baseDirection.X + (float)(lambda * (surface.X - localTiltPoint.X)),
            baseDirection.Y + (float)(lambda * (surface.Y - localTiltPoint.Y)),
            baseDirection.Z + (float)(lambda * (surface.Z - localTiltPoint.Z)));

        if (raw.LengthSquared() <= 0f)
        {
            throw new InvalidOperationException("Modeled direction collapsed to zero vector.");
        }

        var normalized = Vector3.Normalize(raw);
        return new Vector3D(normalized.X, normalized.Y, normalized.Z);
    }

    public static double PointToRayError(Point3 localHole, double u, double theta, double lambda, IAxisymmetricSourceProfile profile, Point3 localTiltPoint)
    {
        var source = ParameterizeSurface(profile, u, theta);
        var direction = BuildModeledDirection(profile, u, theta, lambda, localTiltPoint);

        var px = localHole.X - source.X;
        var py = localHole.Y - source.Y;
        var pz = localHole.Z - source.Z;

        var t = Math.Max(0d, (px * direction.X) + (py * direction.Y) + (pz * direction.Z));
        var rx = px - (t * direction.X);
        var ry = py - (t * direction.Y);
        var rz = pz - (t * direction.Z);

        return (rx * rx) + (ry * ry) + (rz * rz);
    }

    private static double ComputeRegularity(IReadOnlyList<AxisymmetricProjectionPoint> points)
    {
        if (points.Count <= 1)
        {
            return 0d;
        }

        var nearest = new double[points.Count];
        Array.Fill(nearest, double.MaxValue);

        for (var i = 0; i < points.Count; i++)
        {
            var ui = points[i].UnwrappedU ?? 0d;
            var vi = points[i].UnwrappedV ?? 0d;
            for (var j = i + 1; j < points.Count; j++)
            {
                var du = ui - (points[j].UnwrappedU ?? 0d);
                var dv = vi - (points[j].UnwrappedV ?? 0d);
                var d = Math.Sqrt((du * du) + (dv * dv));
                if (d < nearest[i]) nearest[i] = d;
                if (d < nearest[j]) nearest[j] = d;
            }
        }

        var mean = nearest.Average();
        var variance = nearest.Select(v => (v - mean) * (v - mean)).Average();
        return variance;
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

    public static double WrapTheta(double theta)
    {
        var wrapped = theta % TwoPi;
        if (wrapped < 0d)
        {
            wrapped += TwoPi;
        }

        return wrapped;
    }

    private sealed record CandidateResult(double Lambda, double Score, IReadOnlyList<AxisymmetricProjectionPoint> Points);
    private sealed record SolvedPoint(double U, double Theta, Point3 SourceLocal, Vector3D ModeledLocalDirection, double FitError);
}

public sealed record SelfCalibratingSolveResult(
    double EstimatedTiltWeight,
    IReadOnlyList<AxisymmetricProjectionPoint> Points,
    SelfCalibratingAxisymmetricProjectionDiagnostics Diagnostics);
