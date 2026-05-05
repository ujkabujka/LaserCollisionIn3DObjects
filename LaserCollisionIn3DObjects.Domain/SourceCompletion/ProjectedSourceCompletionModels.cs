using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.SourceCompletion;

public sealed record ProjectedSourceCompletionRequest(
    string Name,
    AxisymmetricSourceProfileDefinition ProfileDefinition,
    PointSourceFrameState SourceFrame,
    IReadOnlyList<ProjectionRay> Rays);

public sealed record SourceCompletionSettings(
    double AngularStepDegrees,
    double GapThresholdDegrees,
    bool IncludeOriginalRays,
    int? MaxSyntheticRays = null);

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
    int SyntheticRayCount);
