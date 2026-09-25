using LaserCollisionIn3DObjects.Domain.Graphing;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;

public enum SimilarityMetric
{
    CosineSimilarity,
    HistogramIntersection,
    JensenShannonSimilarity,
}

public sealed record SimilarityMetricOption(SimilarityMetric Metric, string DisplayName);

public sealed class SimilarityPairViewModel
{
    public required SourceHistogramSimilarityResult Result { get; init; }
    public string SourceA => Result.SourceAName;
    public string SourceB => Result.SourceBName;
    public string Cosine => Format(Result.Metrics.CosineSimilarity);
    public string Intersection => Format(Result.Metrics.HistogramIntersection);
    public string JensenShannon => Format(Result.Metrics.JensenShannonSimilarity);

    internal static string Format(double? value) => value.HasValue && double.IsFinite(value.Value)
        ? value.Value.ToString("0.0000")
        : "N/A";
}
