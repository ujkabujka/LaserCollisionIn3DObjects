namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed record HistogramSimilarityMetrics(
    double? CosineSimilarity,
    double? HistogramIntersection,
    double? JensenShannonSimilarity)
{
    public bool IsAvailable => CosineSimilarity.HasValue;
}

public sealed record SourceHistogramSimilarityResult(
    string SourceAId,
    string SourceAName,
    string SourceBId,
    string SourceBName,
    HistogramSimilarityMetrics Metrics);

public sealed record SourceHistogramData(
    string SourceId,
    string SourceName,
    IReadOnlyList<AngleBinCount> Bins)
{
    public bool HasRays => Bins.Sum(bin => (long)bin.Count) > 0;
}

public sealed record AngleHistogramSimilarityAnalysis(
    double BinSizeDeg,
    IReadOnlyList<SourceHistogramData> Sources,
    IReadOnlyList<SourceHistogramSimilarityResult> Pairs);

/// <summary>Compares compatible angle-count histograms by distribution shape.</summary>
public sealed class AngleHistogramSimilarityService
{
    private const double BinTolerance = 1e-9;

    public HistogramSimilarityMetrics Compare(
        IReadOnlyList<AngleBinCount> first,
        IReadOnlyList<AngleBinCount> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ValidateCompatibility(first, second);

        var firstTotal = first.Sum(bin => (long)bin.Count);
        var secondTotal = second.Sum(bin => (long)bin.Count);
        if (firstTotal == 0 || secondTotal == 0)
            return new HistogramSimilarityMetrics(null, null, null);

        double dot = 0, firstSquared = 0, secondSquared = 0;
        double intersection = 0, divergence = 0;
        for (var i = 0; i < first.Count; i++)
        {
            var a = (double)first[i].Count;
            var b = (double)second[i].Count;
            dot += a * b;
            firstSquared += a * a;
            secondSquared += b * b;

            var p = a / firstTotal;
            var q = b / secondTotal;
            intersection += Math.Min(p, q);
            var midpoint = (p + q) / 2d;
            // Zero-probability KL terms are defined as zero. Base-2 JSD is in [0,1].
            if (p > 0) divergence += 0.5 * p * Math.Log2(p / midpoint);
            if (q > 0) divergence += 0.5 * q * Math.Log2(q / midpoint);
        }

        var cosine = dot / Math.Sqrt(firstSquared * secondSquared);
        return new HistogramSimilarityMetrics(
            Math.Clamp(cosine, 0, 1),
            Math.Clamp(intersection, 0, 1),
            Math.Clamp(1d - divergence, 0, 1));
    }

    private static void ValidateCompatibility(IReadOnlyList<AngleBinCount> first, IReadOnlyList<AngleBinCount> second)
    {
        if (first.Count != second.Count)
            throw new ArgumentException("Histograms must have the same bin count.", nameof(second));

        for (var i = 0; i < first.Count; i++)
        {
            if (!NearlyEqual(first[i].BinStartInclusiveDeg, second[i].BinStartInclusiveDeg)
                || !NearlyEqual(first[i].BinEndDeg, second[i].BinEndDeg)
                || !NearlyEqual(first[i].BinCenterDeg, second[i].BinCenterDeg))
                throw new ArgumentException($"Histograms have incompatible bin boundaries at index {i}.", nameof(second));
        }
    }

    private static bool NearlyEqual(double first, double second) => Math.Abs(first - second) <= BinTolerance;
}

/// <summary>Builds each source histogram once, then calculates every unique source pair.</summary>
public sealed class AngleHistogramSimilarityAnalysisService
{
    private readonly AngleHistogramService _histograms = new();
    private readonly AngleHistogramSimilarityService _similarity = new();

    public AngleHistogramSimilarityAnalysis Analyze(IReadOnlyList<GraphableSourceData> sources, double binSizeDeg)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count < 2) throw new ArgumentException("At least two sources are required.", nameof(sources));

        var histograms = sources.Select(source => new SourceHistogramData(
            source.Id,
            source.DisplayName,
            _histograms.CreateHistogram(source.Rays, source.AxisX, binSizeDeg))).ToList();
        var pairs = new List<SourceHistogramSimilarityResult>(sources.Count * (sources.Count - 1) / 2);
        for (var first = 0; first < histograms.Count; first++)
        for (var second = first + 1; second < histograms.Count; second++)
            pairs.Add(new SourceHistogramSimilarityResult(
                histograms[first].SourceId, histograms[first].SourceName,
                histograms[second].SourceId, histograms[second].SourceName,
                _similarity.Compare(histograms[first].Bins, histograms[second].Bins)));

        return new AngleHistogramSimilarityAnalysis(binSizeDeg, histograms, pairs);
    }
}
