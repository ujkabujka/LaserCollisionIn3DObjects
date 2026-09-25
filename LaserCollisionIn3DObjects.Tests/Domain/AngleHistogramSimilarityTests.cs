using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class AngleHistogramSimilarityTests
{
    private readonly AngleHistogramSimilarityService _service = new();

    [Fact]
    public void IdenticalHistogramsHaveUnitSimilarity()
    {
        var metrics = _service.Compare(Bins(10, 20, 30), Bins(10, 20, 30));
        AssertUnit(metrics);
    }

    [Fact]
    public void ScalingRayCountDoesNotChangeHistogramShapeSimilarity()
    {
        var metrics = _service.Compare(Bins(1, 2, 3), Bins(10, 20, 30));
        AssertUnit(metrics);
    }

    [Fact]
    public void DisjointHistogramsHaveZeroSimilarity()
    {
        var metrics = _service.Compare(Bins(10, 0, 0), Bins(0, 0, 10));
        Assert.Equal(0, metrics.CosineSimilarity!.Value, 12);
        Assert.Equal(0, metrics.HistogramIntersection!.Value, 12);
        Assert.Equal(0, metrics.JensenShannonSimilarity!.Value, 12);
    }

    [Fact]
    public void PartialOverlapUsesNormalizedIntersectionAndBase2JensenShannonSimilarity()
    {
        var metrics = _service.Compare(Bins(1, 1), Bins(1, 0));
        Assert.Equal(Math.Sqrt(0.5), metrics.CosineSimilarity!.Value, 12);
        Assert.Equal(0.5, metrics.HistogramIntersection!.Value, 12);
        Assert.Equal(0.6887218755408672, metrics.JensenShannonSimilarity!.Value, 12);
    }

    [Fact]
    public void EmptyHistogramReturnsUnavailableMetrics()
    {
        var metrics = _service.Compare(Bins(0, 0), Bins(1, 0));
        Assert.False(metrics.IsAvailable);
        Assert.Null(metrics.CosineSimilarity);
        Assert.Null(metrics.HistogramIntersection);
        Assert.Null(metrics.JensenShannonSimilarity);
    }

    [Fact]
    public void IncompatibleBinsAreRejectedClearly()
    {
        var incompatible = new[] { new AngleBinCount(0, 20, 10, 1), new AngleBinCount(20, 40, 30, 1) };
        var error = Assert.Throws<ArgumentException>(() => _service.Compare(Bins(1, 1), incompatible));
        Assert.Contains("incompatible", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalysisUsesRealAngleHistogramsAndHonorsBinSize()
    {
        var first = Source("a", "A", RayAtDegrees(5));
        var second = Source("b", "B", RayAtDegrees(15));
        var analysis = new AngleHistogramSimilarityAnalysisService();

        var fine = analysis.Analyze([first, second], 1);
        var coarse = analysis.Analyze([first, second], 20);

        Assert.Equal(180, fine.Sources[0].Bins.Count);
        Assert.Equal(9, coarse.Sources[0].Bins.Count);
        Assert.NotEqual(fine.Pairs[0].Metrics.CosineSimilarity, coarse.Pairs[0].Metrics.CosineSimilarity);
        Assert.Equal(0, fine.Pairs[0].Metrics.CosineSimilarity);
        Assert.Equal(1, coarse.Pairs[0].Metrics.CosineSimilarity);
    }

    [Fact]
    public void FourSourcesProduceSixUniquePairs()
    {
        var sources = Enumerable.Range(0, 4).Select(index => Source(index.ToString(), $"Source {index}", Vector3.UnitX)).ToList();
        var result = new AngleHistogramSimilarityAnalysisService().Analyze(sources, 10);

        Assert.Equal(6, result.Pairs.Count);
        Assert.Equal(6, result.Pairs.Select(pair => new HashSet<string> { pair.SourceAId, pair.SourceBId }).Select(set => string.Join("|", set.Order())).Distinct().Count());
        Assert.DoesNotContain(result.Pairs, pair => pair.SourceAId == pair.SourceBId);
    }

    private static IReadOnlyList<AngleBinCount> Bins(params int[] counts) => counts
        .Select((count, index) => new AngleBinCount(index * 10, (index + 1) * 10, index * 10 + 5, count)).ToList();

    private static void AssertUnit(HistogramSimilarityMetrics metrics)
    {
        Assert.Equal(1, metrics.CosineSimilarity!.Value, 12);
        Assert.Equal(1, metrics.HistogramIntersection!.Value, 12);
        Assert.Equal(1, metrics.JensenShannonSimilarity!.Value, 12);
    }

    private static GraphableSourceData Source(string id, string name, params Vector3[] directions) => new()
    {
        Id = id, DisplayName = name, Kind = GraphableSourceKind.ImportedLightSource,
        AxisX = Vector3.UnitX, AxisY = Vector3.UnitY, AxisZ = Vector3.UnitZ,
        Rays = directions.Select(direction => new Ray3D(Vector3.Zero, direction)).ToList(),
    };

    private static Vector3 RayAtDegrees(double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return Vector3.Normalize(new Vector3((float)Math.Cos(radians), (float)Math.Sin(radians), 0));
    }
}
