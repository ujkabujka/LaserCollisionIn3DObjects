using LaserCollisionIn3DObjects.Domain.Import;

namespace LaserCollisionIn3DObjects.Tests.Import;

public sealed class AnnotationGeometryKeyTests
{
    [Fact]
    public void IdenticalHoleCirclesAreDuplicates()
    {
        var seen = new HashSet<AnnotationGeometryKey>();

        Assert.True(seen.Add(AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, 100, 100, 8)));
        Assert.False(seen.Add(AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, 100, 100, 8)));
    }

    [Fact]
    public void IdenticalNaturalCirclesAreDuplicates()
    {
        Assert.Equal(
            AnnotationGeometryKey.Circle(AnnotationSemanticType.Natural, 100, 100, 8),
            AnnotationGeometryKey.Circle(AnnotationSemanticType.Natural, 100, 100, 8));
    }

    [Fact]
    public void IdenticalGeometryInDifferentCategoriesIsDistinct()
    {
        Assert.NotEqual(
            AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, 100, 100, 8),
            AnnotationGeometryKey.Circle(AnnotationSemanticType.Natural, 100, 100, 8));
    }

    [Theory]
    [InlineData(101, 100, 8)]
    [InlineData(100, 100, 9)]
    public void NearOrDifferentRadiusCirclesAreDistinct(double x, double y, double radius)
    {
        Assert.NotEqual(
            AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, 100, 100, 8),
            AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, x, y, radius));
    }

    [Fact]
    public void DifferentShapeAtSameCenterIsDistinct()
    {
        var circle = AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, 100, 100, 8);
        var polygon = AnnotationGeometryKey.Polygon(AnnotationSemanticType.Hole,
            [new(99, 99), new(101, 99), new(101, 101), new(99, 101)]);

        Assert.NotEqual(circle, polygon);
    }

    [Fact]
    public void PolygonCyclicRotationIsDuplicate()
    {
        Assert.Equal(Polygon(A, B, C, D), Polygon(C, D, A, B));
    }

    [Fact]
    public void PolygonReversedWindingIsDuplicate()
    {
        Assert.Equal(Polygon(A, B, C, D), Polygon(D, C, B, A));
    }

    [Fact]
    public void PolygonWithChangedVertexIsDistinct()
    {
        Assert.NotEqual(Polygon(A, B, C, D), Polygon(A, B, C, new(0, 2)));
    }

    [Fact]
    public void ExactRepeatedClosingVertexDoesNotChangePolygonIdentity()
    {
        Assert.Equal(Polygon(A, B, C, D), Polygon(A, B, C, D, A));
    }

    [Fact]
    public void RepeatedRealVertexIsNotDiscarded()
    {
        Assert.NotEqual(Polygon(A, B, C, D), Polygon(A, B, C, B, D));
    }

    [Fact]
    public void LargeDuplicatedSourcePatternKeepsOnlyUniqueSemanticGeometry()
    {
        var source = Enumerable.Range(0, 250)
            .Select(index => AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, index, index + 1, 8))
            .Concat(Enumerable.Range(0, 100).Select(index => AnnotationGeometryKey.Ellipse(AnnotationSemanticType.Natural, index, index + 1, 3, 4)))
            .ToArray();
        var duplicated = source.Concat(source).ToArray();

        Assert.Equal(source.Length, duplicated.ToHashSet().Count);
    }

    [Fact]
    public void DetectorAggregatesLargeDuplicateSetsIntoTwoDiagnostics()
    {
        var detector = new AnnotationDuplicateDetector();
        var hole = AnnotationGeometryKey.Circle(AnnotationSemanticType.Hole, 10, 20, 3);
        var natural = AnnotationGeometryKey.Ellipse(AnnotationSemanticType.Natural, 30, 40, 5, 6);

        Assert.True(detector.TryAccept(hole));
        Assert.True(detector.TryAccept(natural));
        for (var i = 0; i < 100; i++) Assert.False(detector.TryAccept(hole));
        for (var i = 0; i < 20; i++) Assert.False(detector.TryAccept(natural));

        Assert.Equal(101, detector.RawHoleCount);
        Assert.Equal(21, detector.RawNaturalCount);
        Assert.Equal(100, detector.RemovedHoleCount);
        Assert.Equal(20, detector.RemovedNaturalCount);
        Assert.Equal(
            ["Removed 100 duplicate Hole annotations.", "Removed 20 duplicate Natural annotations."],
            detector.CreateDiagnostics());
    }

    private static readonly AnnotationVertex A = new(0, 0);
    private static readonly AnnotationVertex B = new(1, 0);
    private static readonly AnnotationVertex C = new(1, 1);
    private static readonly AnnotationVertex D = new(0, 1);

    private static AnnotationGeometryKey Polygon(params AnnotationVertex[] points)
        => AnnotationGeometryKey.Polygon(AnnotationSemanticType.Hole, points);
}
