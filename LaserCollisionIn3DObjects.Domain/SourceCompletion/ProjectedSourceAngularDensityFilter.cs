using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.SourceCompletion;

/// <summary>Classifies rays for completion inference without mutating the physical projection.</summary>
public sealed class ProjectedSourceAngularDensityFilter
{
    public AngularOutlierFilterResult Filter(
        ProjectedSourceCompletionRequest request,
        AngularOutlierFilterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);

        if (!settings.Enabled || request.Rays.Count == 0)
        {
            return PreserveAll(request.Rays);
        }

        var binCount = Math.Max(1, (int)Math.Ceiling(360d / settings.BinWidthDegrees));
        var rayBins = new int[request.Rays.Count];
        var counts = new int[binCount];
        for (var i = 0; i < request.Rays.Count; i++)
        {
            var local = ProjectedSourceFrameMath.WorldPointToLocal(request.Rays[i].Ray.Origin, request.SourceFrame);
            var theta = ProjectedSourceFrameMath.NormalizeDegrees(ProjectedSourceFrameMath.ComputeThetaDegrees(local));
            // Center bins on multiples of the configured width. This avoids splitting populations
            // around integer-degree centers and joins the circular 359/0 boundary.
            var bin = (int)Math.Floor((theta + (settings.BinWidthDegrees / 2d)) / settings.BinWidthDegrees) % binCount;
            rayBins[i] = bin;
            counts[bin]++;
        }

        var maximum = counts.Max();
        var required = Math.Max(settings.MinimumSamplesPerBin,
            (int)Math.Ceiling(maximum * settings.MinimumRelativeSupport));
        if (maximum < required)
        {
            return PreserveAll(request.Rays);
        }

        var inliers = new List<ProjectionRay>(request.Rays.Count);
        var rejected = new List<ProjectionRay>();
        for (var i = 0; i < request.Rays.Count; i++)
        {
            (counts[rayBins[i]] >= required ? inliers : rejected).Add(request.Rays[i]);
        }

        var bins = counts.Select((count, index) => new AngularBinStatistics(
            index,
            ProjectedSourceFrameMath.NormalizeDegrees((index * settings.BinWidthDegrees) - (settings.BinWidthDegrees / 2d)),
            ProjectedSourceFrameMath.NormalizeDegrees((index * settings.BinWidthDegrees) + (settings.BinWidthDegrees / 2d)),
            count,
            count >= required)).ToList();
        return new AngularOutlierFilterResult(inliers, rejected, bins, required, true);
    }

    private static AngularOutlierFilterResult PreserveAll(IReadOnlyList<ProjectionRay> rays)
        => new(rays.ToList(), Array.Empty<ProjectionRay>(), Array.Empty<AngularBinStatistics>(), 0, false);

    internal static void Validate(AngularOutlierFilterSettings settings)
    {
        if (!double.IsFinite(settings.BinWidthDegrees) || settings.BinWidthDegrees <= 0d || settings.BinWidthDegrees > 360d)
            throw new ArgumentOutOfRangeException(nameof(settings.BinWidthDegrees), "Angular bin width must be greater than 0 and at most 360 degrees.");
        if (settings.MinimumSamplesPerBin < 1)
            throw new ArgumentOutOfRangeException(nameof(settings.MinimumSamplesPerBin), "Minimum samples per bin must be at least 1.");
        if (!double.IsFinite(settings.MinimumRelativeSupport) || settings.MinimumRelativeSupport < 0d || settings.MinimumRelativeSupport > 1d)
            throw new ArgumentOutOfRangeException(nameof(settings.MinimumRelativeSupport), "Minimum relative support must be between 0 and 1.");
    }
}
