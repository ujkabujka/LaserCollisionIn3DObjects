using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.SourceCompletion;

public sealed class ProjectedSourceAzimuthAnalyzer
{
    public IReadOnlyList<AzimuthCoverageInterval> DetectCoverage(
        ProjectedSourceCompletionRequest request,
        double gapThresholdDegrees)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (gapThresholdDegrees <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(gapThresholdDegrees), gapThresholdDegrees, "Gap threshold must be positive.");
        }

        var thetas = request.Rays.Select(ray =>
            ProjectedSourceFrameMath.NormalizeDegrees(
                ProjectedSourceFrameMath.ComputeThetaDegrees(
                    ProjectedSourceFrameMath.WorldPointToLocal(ray.Ray.Origin, request.SourceFrame)))).OrderBy(v => v).ToList();

        if (thetas.Count == 0)
        {
            return Array.Empty<AzimuthCoverageInterval>();
        }

        if (thetas.Count == 1)
        {
            return new[] { new AzimuthCoverageInterval(thetas[0], thetas[0], 1) };
        }

        var doubled = thetas.Concat(thetas.Select(v => v + 360d)).ToArray();
        var breaks = new List<int>();
        for (var i = 0; i < thetas.Count; i++)
        {
            var delta = doubled[i + 1] - doubled[i];
            if (delta > gapThresholdDegrees)
            {
                breaks.Add(i);
            }
        }

        if (breaks.Count == 0)
        {
            return new[] { new AzimuthCoverageInterval(thetas[0], thetas[^1], thetas.Count) };
        }

        var intervals = new List<AzimuthCoverageInterval>();
        for (var b = 0; b < breaks.Count; b++)
        {
            var startIndex = (breaks[b] + 1) % thetas.Count;
            var endIndex = breaks[(b + 1) % breaks.Count];
            var count = endIndex >= startIndex
                ? (endIndex - startIndex + 1)
                : ((thetas.Count - startIndex) + endIndex + 1);
            intervals.Add(new AzimuthCoverageInterval(thetas[startIndex], thetas[endIndex], count));
        }

        return intervals.OrderBy(i => i.StartDegrees).ToList();
    }

    public IReadOnlyList<AzimuthGapInterval> DetectGaps(
        ProjectedSourceCompletionRequest request,
        double gapThresholdDegrees)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (gapThresholdDegrees <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(gapThresholdDegrees), gapThresholdDegrees, "Gap threshold must be positive.");
        }

        var thetas = request.Rays.Select(ray =>
            ProjectedSourceFrameMath.NormalizeDegrees(
                ProjectedSourceFrameMath.ComputeThetaDegrees(
                    ProjectedSourceFrameMath.WorldPointToLocal(ray.Ray.Origin, request.SourceFrame)))).OrderBy(v => v).ToList();

        if (thetas.Count < 2)
        {
            return Array.Empty<AzimuthGapInterval>();
        }

        var gaps = new List<AzimuthGapInterval>();
        for (var i = 0; i < thetas.Count; i++)
        {
            var start = thetas[i];
            var next = i == thetas.Count - 1 ? thetas[0] + 360d : thetas[i + 1];
            var width = next - start;
            if (width <= gapThresholdDegrees)
            {
                continue;
            }

            AddNonWrappingGap(gaps, start, next, width);
        }

        return gaps.OrderBy(g => g.StartDegrees).ToList();
    }

    private static void AddNonWrappingGap(List<AzimuthGapInterval> gaps, double startDegrees, double endDegrees, double widthDegrees)
    {
        if (endDegrees <= 360d)
        {
            gaps.Add(new AzimuthGapInterval(startDegrees, endDegrees, widthDegrees));
            return;
        }

        gaps.Add(new AzimuthGapInterval(startDegrees, 360d, 360d - startDegrees));
        var wrappedEnd = endDegrees - 360d;
        gaps.Add(new AzimuthGapInterval(0d, wrappedEnd, wrappedEnd));
    }
}

public sealed class ProjectedSourceCompletionService
{
    private const float SyntheticTargetDistance = 1000f;
    private readonly ProjectedSourceAzimuthAnalyzer _analyzer = new();

    public ProjectedSourceCompletionResult CompleteByRotationalCopy(
        ProjectedSourceCompletionRequest request,
        SourceCompletionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);

        if (request.Rays.Count == 0)
        {
            throw new ArgumentException("Projected source completion requires at least one ray.", nameof(request));
        }

        if (settings.AngularStepDegrees <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.AngularStepDegrees), settings.AngularStepDegrees, "Angular step must be positive.");
        }

        if (settings.GapThresholdDegrees <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.GapThresholdDegrees), settings.GapThresholdDegrees, "Gap threshold must be positive.");
        }

        if (settings.MaxSyntheticRays is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settings.MaxSyntheticRays), settings.MaxSyntheticRays, "Max synthetic rays cannot be negative.");
        }

        var profile = request.ProfileDefinition.BuildProfile();
        var samples = request.Rays.Select(ray => BuildLocalSample(ray, request.SourceFrame, profile)).ToList();
        var coverage = _analyzer.DetectCoverage(request, settings.GapThresholdDegrees);
        var gaps = _analyzer.DetectGaps(request, settings.GapThresholdDegrees);

        var synthetic = new List<ProjectionRay>();
        foreach (var gap in gaps)
        {
            foreach (var targetTheta in EnumerateGapTargets(gap, settings.AngularStepDegrees))
            {
                if (settings.MaxSyntheticRays.HasValue && synthetic.Count >= settings.MaxSyntheticRays.Value)
                {
                    break;
                }

                var sourceSample = FindNearestSample(samples, targetTheta);
                var deltaDegrees = ProjectedSourceFrameMath.NormalizeDeltaDegrees(targetTheta - sourceSample.ThetaDegrees);
                var localDirection = RotateAroundLocalX(sourceSample.LocalDirection, deltaDegrees);
                localDirection = ProjectedSourceFrameMath.NormalizeDirection(localDirection, "Synthetic local direction cannot be zero.");

                var u = Math.Clamp(sourceSample.U, 0d, profile.Length);
                var targetThetaRadians = (float)(targetTheta * Math.PI / 180d);
                var localOrigin = profile.EvaluateSurfacePoint((float)u, targetThetaRadians);

                var worldOrigin = ProjectedSourceFrameMath.LocalPointToWorld(localOrigin, request.SourceFrame);
                var worldDirection = ProjectedSourceFrameMath.LocalDirectionToWorld(localDirection, request.SourceFrame);
                worldDirection = ProjectedSourceFrameMath.NormalizeDirection(worldDirection, "Synthetic world direction cannot be zero.");

                var ray = new Ray3D(worldOrigin, worldDirection);
                var targetPointVector = worldOrigin + (worldDirection * SyntheticTargetDistance);
                var targetPoint = new Point3(targetPointVector.X, targetPointVector.Y, targetPointVector.Z);
                synthetic.Add(new ProjectionRay(ray, targetPoint));
            }

            if (settings.MaxSyntheticRays.HasValue && synthetic.Count >= settings.MaxSyntheticRays.Value)
            {
                break;
            }
        }

        var output = settings.IncludeOriginalRays
            ? request.Rays.Concat(synthetic).ToList()
            : synthetic;

        return new ProjectedSourceCompletionResult(
            $"Completed - {request.Name}",
            output,
            coverage,
            gaps,
            request.Rays.Count,
            synthetic.Count);
    }

    private static IEnumerable<double> EnumerateGapTargets(AzimuthGapInterval gap, double stepDegrees)
    {
        var current = gap.StartDegrees + stepDegrees;
        while (current < gap.EndDegrees)
        {
            yield return ProjectedSourceFrameMath.NormalizeDegrees(current);
            current += stepDegrees;
        }
    }

    private static ProjectedRayLocalSample FindNearestSample(IReadOnlyList<ProjectedRayLocalSample> samples, double thetaDegrees)
    {
        return samples
            .OrderBy(sample => ProjectedSourceFrameMath.CircularDistanceDegrees(sample.ThetaDegrees, thetaDegrees))
            .First();
    }

    private static Vector3 RotateAroundLocalX(Vector3 direction, double deltaDegrees)
    {
        var radians = (float)(deltaDegrees * Math.PI / 180d);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector3(
            direction.X,
            (direction.Y * cos) - (direction.Z * sin),
            (direction.Y * sin) + (direction.Z * cos));
    }

    private static ProjectedRayLocalSample BuildLocalSample(ProjectionRay ray, PointSourceFrameState frame, IAxisymmetricSourceProfile profile)
    {
        var localOrigin = ProjectedSourceFrameMath.WorldPointToLocal(ray.Ray.Origin, frame);
        var localDirection = ProjectedSourceFrameMath.WorldDirectionToLocal(ray.Ray.Direction, frame);
        localDirection = ProjectedSourceFrameMath.NormalizeDirection(localDirection, "Ray direction must be non-zero.");

        var theta = ProjectedSourceFrameMath.NormalizeDegrees(ProjectedSourceFrameMath.ComputeThetaDegrees(localOrigin));
        var u = Math.Clamp(localOrigin.X, 0d, profile.Length);
        return new ProjectedRayLocalSample(ray, u, theta, localDirection);
    }

    private sealed record ProjectedRayLocalSample(
        ProjectionRay OriginalRay,
        double U,
        double ThetaDegrees,
        Vector3 LocalDirection);
}

internal static class ProjectedSourceFrameMath
{
    public static Vector3 WorldPointToLocal(Vector3 worldPoint, PointSourceFrameState frame)
    {
        var origin = ToVector3(frame.Origin);
        var axisX = ToVector3(frame.AxisX);
        var axisY = ToVector3(frame.AxisY);
        var axisZ = ToVector3(frame.AxisZ);
        var delta = worldPoint - origin;
        return new Vector3(Vector3.Dot(delta, axisX), Vector3.Dot(delta, axisY), Vector3.Dot(delta, axisZ));
    }

    public static Vector3 WorldDirectionToLocal(Vector3 worldDirection, PointSourceFrameState frame)
    {
        var axisX = ToVector3(frame.AxisX);
        var axisY = ToVector3(frame.AxisY);
        var axisZ = ToVector3(frame.AxisZ);
        return new Vector3(Vector3.Dot(worldDirection, axisX), Vector3.Dot(worldDirection, axisY), Vector3.Dot(worldDirection, axisZ));
    }

    public static Vector3 LocalPointToWorld(Vector3 localPoint, PointSourceFrameState frame)
    {
        var origin = ToVector3(frame.Origin);
        var axisX = ToVector3(frame.AxisX);
        var axisY = ToVector3(frame.AxisY);
        var axisZ = ToVector3(frame.AxisZ);
        return origin + (localPoint.X * axisX) + (localPoint.Y * axisY) + (localPoint.Z * axisZ);
    }

    public static Vector3 LocalDirectionToWorld(Vector3 localDirection, PointSourceFrameState frame)
    {
        var axisX = ToVector3(frame.AxisX);
        var axisY = ToVector3(frame.AxisY);
        var axisZ = ToVector3(frame.AxisZ);
        return (localDirection.X * axisX) + (localDirection.Y * axisY) + (localDirection.Z * axisZ);
    }

    public static Vector3 NormalizeDirection(Vector3 direction, string errorMessage)
    {
        if (float.IsNaN(direction.X) || float.IsNaN(direction.Y) || float.IsNaN(direction.Z) || direction.LengthSquared() <= 0f)
        {
            throw new ArgumentException(errorMessage);
        }

        return Vector3.Normalize(direction);
    }

    public static double ComputeThetaDegrees(Vector3 localPoint)
    {
        return Math.Atan2(localPoint.Z, localPoint.Y) * (180d / Math.PI);
    }

    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360d;
        if (normalized < 0d)
        {
            normalized += 360d;
        }

        return normalized;
    }

    public static double NormalizeDeltaDegrees(double degrees)
    {
        var normalized = ((degrees + 180d) % 360d + 360d) % 360d - 180d;
        return normalized;
    }

    public static double CircularDistanceDegrees(double a, double b)
    {
        var delta = Math.Abs(a - b);
        return Math.Min(delta, 360d - delta);
    }

    private static Vector3 ToVector3(Point3 point) => new((float)point.X, (float)point.Y, (float)point.Z);
    private static Vector3 ToVector3(Vector3D vector) => new((float)vector.X, (float)vector.Y, (float)vector.Z);
}
