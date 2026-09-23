using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.SourceCompletion;

public enum SourceCompletionMethod
{
    RotationalCopy,
    Mirror,
}

/// <summary>Stable ownership identity for a saved projection result used by source completion.</summary>
public readonly record struct ProjectedSourceIdentity(string SceneName, string ProjectionResultKey)
{
    public override string ToString() => $"{SceneName}\u001f{ProjectionResultKey}";
}

public sealed record ProjectedSourceCompletionRequest(
    string Name,
    AxisymmetricSourceProfileDefinition ProfileDefinition,
    PointSourceFrameState SourceFrame,
    IReadOnlyList<ProjectionRay> Rays);

public sealed record SourceCompletionSettings(
    double AngularStepDegrees,
    double GapThresholdDegrees,
    bool IncludeOriginalRays,
    int? MaxSyntheticRays = null,
    SourceCompletionMethod Method = SourceCompletionMethod.RotationalCopy,
    double MirrorAxisDegrees = 0d,
    AngularOutlierFilterSettings? AngularOutlierFilter = null)
{
    public AngularOutlierFilterSettings EffectiveAngularOutlierFilter => AngularOutlierFilter ?? new();
}

public sealed record AngularOutlierFilterSettings(
    bool Enabled = true,
    double BinWidthDegrees = 1d,
    int MinimumSamplesPerBin = 5,
    double MinimumRelativeSupport = 0.05d);

public sealed record AngularBinStatistics(
    int BinIndex,
    double StartDegrees,
    double EndDegrees,
    int SampleCount,
    bool IsAccepted);

public sealed record AngularOutlierFilterResult(
    IReadOnlyList<ProjectionRay> InlierRays,
    IReadOnlyList<ProjectionRay> RejectedRays,
    IReadOnlyList<AngularBinStatistics> Bins,
    int RequiredSupport,
    bool FilterApplied);

public sealed record AzimuthCoverageInterval(
    double StartDegrees,
    double EndDegrees,
    int SampleCount);

public sealed record AzimuthGapInterval(
    double StartDegrees,
    double EndDegrees,
    double WidthDegrees);

public sealed record ProjectedSourceCompletionResult(
    string Name,
    IReadOnlyList<ProjectionRay> Rays,
    IReadOnlyList<AzimuthCoverageInterval> CoverageIntervals,
    IReadOnlyList<AzimuthGapInterval> GapIntervals,
    int OriginalRayCount,
    int SyntheticRayCount,
    int AnalysisRayCount,
    IReadOnlyList<ProjectionRay> RejectedOutlierRays,
    IReadOnlyList<ProjectionRay> SyntheticRays)
{
    public int RejectedOutlierCount => RejectedOutlierRays.Count;
}

public sealed record ProjectedSourceAnalysisResult(
    IReadOnlyList<AzimuthCoverageInterval> CoverageIntervals,
    IReadOnlyList<AzimuthGapInterval> GapIntervals,
    AngularOutlierFilterResult FilterResult)
{
    public int OriginalRayCount => FilterResult.InlierRays.Count + FilterResult.RejectedRays.Count;
    public int AnalysisRayCount => FilterResult.InlierRays.Count;
    public int RejectedOutlierCount => FilterResult.RejectedRays.Count;
}
