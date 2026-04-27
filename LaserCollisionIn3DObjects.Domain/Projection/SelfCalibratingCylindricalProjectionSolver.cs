using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed class SelfCalibratingCylindricalProjectionSolver
{
    private const double TwoPi = Math.PI * 2d;
    private const double Epsilon = 1e-9;

    public SelfCalibratingCylindricalProjectionSolverSettings Settings { get; }

    public SelfCalibratingCylindricalProjectionSolver(SelfCalibratingCylindricalProjectionSolverSettings? settings = null)
    {
        Settings = settings ?? SelfCalibratingCylindricalProjectionSolverSettings.Default;
    }

    public SelfCalibratingSolveResult Solve(
        IReadOnlyList<Point3> localHolePoints,
        PointSourceFrameState frame,
        double radius,
        double length,
        Point3 localTiltPoint,
        IReadOnlyList<Point3> worldHolePoints,
        IProgress<ProjectionProgress>? progress = null)
    {
        var scale = Math.Max(Math.Max(radius, length), Math.Max(Math.Sqrt(Math.Pow(localTiltPoint.X - (length * 0.5d), 2d) + Math.Pow(localTiltPoint.Y, 2d) + Math.Pow(localTiltPoint.Z, 2d)), Epsilon));
        var candidateDiagnostics = new List<SelfCalibratingCylindricalCandidateDiagnostics>(Settings.KappaCandidates.Count);

        CandidateResult? best = null;

        for (var c = 0; c < Settings.KappaCandidates.Count; c++)
        {
            var lambda = Settings.KappaCandidates[c] / scale;
            progress?.Report(new ProjectionProgress((100d * c) / Math.Max(Settings.KappaCandidates.Count, 1), $"Testing tilt candidate {c + 1}/{Settings.KappaCandidates.Count}..."));

            var points = new List<CylindricalProjectionPoint>(localHolePoints.Count);
            var fitErrorSum = 0d;

            for (var i = 0; i < localHolePoints.Count; i++)
            {
                if (i % 10 == 0)
                {
                    var coarse = (double)c / Settings.KappaCandidates.Count;
                    var fine = (double)i / Math.Max(localHolePoints.Count, 1);
                    progress?.Report(new ProjectionProgress((coarse + (fine / Settings.KappaCandidates.Count)) * 100d, $"Reconstructing hole {i + 1}/{localHolePoints.Count}..."));
                }

                var solved = SolveSingleHole(localHolePoints[i], lambda, radius, length, localTiltPoint);
                fitErrorSum += solved.FitError;

                var sourceWorld = ToWorld(solved.SourceLocal, frame);
                var modeledWorld = LocalDirectionToWorld(solved.ModeledLocalDirection, frame);
                var actualWorld = BuildNormalizedDirection(sourceWorld, worldHolePoints[i], $"Hole point at index {i} coincides with reconstructed source point.");

                points.Add(new CylindricalProjectionPoint(
                    worldHolePoints[i],
                    sourceWorld,
                    actualWorld,
                    sourceWorld)
                {
                    ModeledRayDirection = modeledWorld,
                    LocalU = solved.U,
                    LocalTheta = solved.Theta,
                    UnwrappedU = solved.U,
                    UnwrappedV = radius * solved.Theta,
                    FitError = solved.FitError,
                });
            }

            var meanFit = fitErrorSum / Math.Max(localHolePoints.Count, 1);
            var regularity = ComputeRegularity(points);
            var score = meanFit + (Settings.RegularityWeight * regularity);
            candidateDiagnostics.Add(new SelfCalibratingCylindricalCandidateDiagnostics(lambda, meanFit, regularity, score));

            if (best is null || score < best.Score)
            {
                best = new CandidateResult(lambda, score, points);
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException("Self-calibrating cylindrical solver failed to evaluate candidates.");
        }

        progress?.Report(new ProjectionProgress(100d, "Projection complete."));

        return new SelfCalibratingSolveResult(
            best.Lambda,
            best.Points,
            new SelfCalibratingCylindricalProjectionDiagnostics
            {
                CandidateScores = candidateDiagnostics,
                RegularityWeight = Settings.RegularityWeight,
            });
    }

    private SolvedPoint SolveSingleHole(Point3 holeLocal, double lambda, double radius, double length, Point3 localTiltPoint)
    {
        var bestU = 0d;
        var bestTheta = 0d;
        var bestErrorSq = double.MaxValue;

        for (var iu = 0; iu < Settings.AxialSamples; iu++)
        {
            var u = (length * iu) / (Settings.AxialSamples - 1d);
            for (var it = 0; it < Settings.AngularSamples; it++)
            {
                var theta = (TwoPi * it) / Settings.AngularSamples;
                var errorSq = PointToRayError(holeLocal, u, theta, lambda, radius, localTiltPoint);
                if (errorSq < bestErrorSq)
                {
                    bestErrorSq = errorSq;
                    bestU = u;
                    bestTheta = theta;
                }
            }
        }

        var uStep = length / Math.Max(1d, Settings.AxialSamples - 1d);
        var tStep = TwoPi / Math.Max(1d, Settings.AngularSamples);

        for (var iteration = 0; iteration < Settings.RefinementIterations; iteration++)
        {
            var candidateImproved = false;
            foreach (var uCandidate in new[] { bestU - uStep, bestU, bestU + uStep })
            {
                var clampedU = Math.Clamp(uCandidate, 0d, length);
                foreach (var tCandidate in new[] { bestTheta - tStep, bestTheta, bestTheta + tStep })
                {
                    var wrappedTheta = WrapTheta(tCandidate);
                    var errorSq = PointToRayError(holeLocal, clampedU, wrappedTheta, lambda, radius, localTiltPoint);
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

        var sourceLocal = ParameterizeSurface(bestU, bestTheta, radius, length);
        var modeledLocalDirection = BuildModeledDirection(bestU, bestTheta, lambda, radius, localTiltPoint);

        return new SolvedPoint(bestU, bestTheta, sourceLocal, modeledLocalDirection, Math.Sqrt(bestErrorSq));
    }

    public static Point3 ParameterizeSurface(double u, double theta, double radius, double length)
    {
        var clampedU = Math.Clamp(u, 0d, length);
        var wrappedTheta = WrapTheta(theta);
        return new Point3(clampedU, radius * Math.Cos(wrappedTheta), radius * Math.Sin(wrappedTheta));
    }

    public static Vector3D BuildModeledDirection(double u, double theta, double lambda, double radius, Point3 localTiltPoint)
    {
        var wrappedTheta = WrapTheta(theta);
        var surface = new Point3(u, radius * Math.Cos(wrappedTheta), radius * Math.Sin(wrappedTheta));
        var raw = new Vector3(
            (float)(lambda * (surface.X - localTiltPoint.X)),
            (float)(Math.Cos(wrappedTheta) + (lambda * (surface.Y - localTiltPoint.Y))),
            (float)(Math.Sin(wrappedTheta) + (lambda * (surface.Z - localTiltPoint.Z))));

        if (raw.LengthSquared() <= 0f)
        {
            throw new InvalidOperationException("Modeled direction collapsed to zero vector.");
        }

        var normalized = Vector3.Normalize(raw);
        return new Vector3D(normalized.X, normalized.Y, normalized.Z);
    }

    public static double PointToRayError(Point3 localHole, double u, double theta, double lambda, double radius, Point3 localTiltPoint)
    {
        var source = ParameterizeSurface(u, theta, radius, length: double.MaxValue);
        var direction = BuildModeledDirection(u, theta, lambda, radius, localTiltPoint);

        var px = localHole.X - source.X;
        var py = localHole.Y - source.Y;
        var pz = localHole.Z - source.Z;

        var t = Math.Max(0d, (px * direction.X) + (py * direction.Y) + (pz * direction.Z));
        var rx = px - (t * direction.X);
        var ry = py - (t * direction.Y);
        var rz = pz - (t * direction.Z);

        return (rx * rx) + (ry * ry) + (rz * rz);
    }

    private static double ComputeRegularity(IReadOnlyList<CylindricalProjectionPoint> points)
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

        var v = Vector3.Normalize(new Vector3((float)worldX, (float)worldY, (float)worldZ));
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

    public static double WrapTheta(double theta)
    {
        var wrapped = theta % TwoPi;
        if (wrapped < 0d)
        {
            wrapped += TwoPi;
        }

        return wrapped;
    }

    private sealed record CandidateResult(double Lambda, double Score, IReadOnlyList<CylindricalProjectionPoint> Points);
    private sealed record SolvedPoint(double U, double Theta, Point3 SourceLocal, Vector3D ModeledLocalDirection, double FitError);
}

public sealed record SelfCalibratingSolveResult(
    double EstimatedTiltWeight,
    IReadOnlyList<CylindricalProjectionPoint> Points,
    SelfCalibratingCylindricalProjectionDiagnostics Diagnostics);
