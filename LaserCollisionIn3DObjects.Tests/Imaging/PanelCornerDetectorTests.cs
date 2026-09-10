using LaserCollisionIn3DObjects.Domain.Imaging;

namespace LaserCollisionIn3DObjects.Tests.Imaging;

public sealed class PanelCornerDetectorTests
{
    [Fact]
    public void Fit_T1P6_PreservesTopLeftAcrossSparseClosingEdge()
    {
        ImagePoint[] contour =
        [
            P(534,85), P(693,83), P(933,86), P(1103,87), P(1360,90), P(1779,87), P(1980,88),
            P(2180,87), P(2351,87), P(2524,86), P(2522,184), P(2521,380), P(2521,531),
            P(2521,647), P(2520,1059), P(2516,1412), P(2516,1735), P(2510,2376), P(2505,2867),
            P(2500,3351), P(2493,3952), P(2365,3950), P(2158,3947), P(1992,3946), P(1794,3945),
            P(1475,3948), P(1266,3952), P(827,3967), P(558,3976),
        ];

        var fitted = PanelCornerDetector.Fit(contour);

        AssertCorners(fitted, P(534, 85), P(2524, 86), P(2493, 3952), P(558, 3976), tolerance: 1);
        Assert.DoesNotContain(fitted, point => Distance(point, P(1360, 90)) < 100);
        Assert.True(Area(fitted) / Area(contour) > 0.95);
    }

    [Fact]
    public void Fit_T1P8_RetainsRealBottomRightAndRejectsStraightEdgePoint()
    {
        ImagePoint[] contour =
        [
            P(464,201), P(464,308), P(462,439), P(459,682), P(454,966), P(449,1232), P(446,1452),
            P(441,1605), P(433,1745), P(422,1949), P(414,2138), P(382,2522), P(367,2665),
            P(354,2866), P(334,3085), P(308,3346), P(290,3531), P(259,3803), P(245,3908),
            P(1031,3929), P(1836,3949), P(2405,3970), P(2374,3452), P(2317,2725), P(2289,2295),
            P(2268,1862), P(2250,1381), P(2239,983), P(2228,603), P(2219,361), P(2209,118),
            P(1775,137), P(1496,151), P(1144,168), P(948,177),
        ];

        var fitted = PanelCornerDetector.Fit(contour);

        AssertCorners(fitted, P(464, 201), P(2209, 118), P(2405, 3970), P(245, 3908), tolerance: 1);
        Assert.DoesNotContain(fitted, point => Distance(point, P(414, 2138)) < 100);
        Assert.True(Area(fitted) / Area(contour) > 0.95);
    }

    [Fact]
    public void Fit_HighlyUnevenSampling_FindsEveryPhysicalCorner()
    {
        var expected = new[] { P(0, 0), P(1000, 0), P(900, 700), P(50, 650) };
        var contour = SampleEdges(expected, 50, 2, 20, 3);
        AssertCorners(PanelCornerDetector.Fit(contour), expected, tolerance: 0.01);
    }

    [Fact]
    public void Fit_ClusteredCornerCandidates_SelectsOnlyOnePointFromBend()
    {
        ImagePoint[] contour =
        [
            P(8,0), P(3,2), P(0,8), P(0,400), P(600,400), P(600,0), P(100,0),
        ];

        var fitted = PanelCornerDetector.Fit(contour);

        Assert.Equal(4, fitted.Count);
        Assert.Single(fitted, point => point.X < 20 && point.Y < 20);
        AssertCorners(fitted, P(3, 2), P(600, 0), P(600, 400), P(0, 400), tolerance: 8);
    }

    [Fact]
    public void Fit_PerspectiveRotatedPanel_HasDeterministicSemanticOrder()
    {
        var expected = new[] { P(300, 200), P(1800, 300), P(2000, 1800), P(150, 1700) };
        var contour = SampleEdges(expected, 31, 3, 17, 5);
        AssertCorners(PanelCornerDetector.Fit(contour), expected, tolerance: 0.01);
    }

    [Fact]
    public void Fit_MildlyNoisyEdges_PrefersMajorBends()
    {
        ImagePoint[] contour =
        [
            P(100,100), P(300,102), P(500,97), P(700,103), P(900,100),
            P(903,300), P(897,500), P(900,700),
            P(700,697), P(500,704), P(300,696), P(100,700),
            P(97,500), P(104,300),
        ];
        AssertCorners(PanelCornerDetector.Fit(contour), P(100, 100), P(900, 100), P(900, 700), P(100, 700), tolerance: 1);
    }

    [Fact]
    public void Fit_DegenerateContour_FailsExplicitly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PanelCornerDetector.Fit([P(0, 0), P(100, 0), P(200, 0), P(300, 0)]));
        Assert.Contains("area", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ImagePoint> SampleEdges(IReadOnlyList<ImagePoint> corners, params int[] samples)
    {
        var result = new List<ImagePoint>();
        for (var edge = 0; edge < 4; edge++)
        {
            var start = corners[edge];
            var end = corners[(edge + 1) % 4];
            for (var sample = 0; sample < samples[edge]; sample++)
            {
                var t = sample / (double)samples[edge];
                result.Add(P(start.X + ((end.X - start.X) * t), start.Y + ((end.Y - start.Y) * t)));
            }
        }
        return result;
    }

    private static void AssertCorners(IReadOnlyList<ImagePoint> actual, params ImagePoint[] expected)
        => AssertCorners(actual, expected, 0.01);

    private static void AssertCorners(IReadOnlyList<ImagePoint> actual, IReadOnlyList<ImagePoint> expected, double tolerance)
    {
        Assert.Equal(4, actual.Count);
        for (var index = 0; index < 4; index++)
            Assert.True(Distance(actual[index], expected[index]) <= tolerance,
                $"Corner {index + 1}: expected {expected[index]}, actual {actual[index]}.");
        Assert.True(Area(actual) > 0);
    }

    private static void AssertCorners(IReadOnlyList<ImagePoint> actual, ImagePoint a, ImagePoint b, ImagePoint c, ImagePoint d, double tolerance)
        => AssertCorners(actual, new[] { a, b, c, d }, tolerance);

    private static ImagePoint P(double x, double y) => new(x, y);
    private static double Distance(ImagePoint a, ImagePoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static double Area(IReadOnlyList<ImagePoint> points)
    {
        var sum = 0d;
        for (var index = 0; index < points.Count; index++)
            sum += (points[index].X * points[(index + 1) % points.Count].Y) - (points[(index + 1) % points.Count].X * points[index].Y);
        return Math.Abs(sum) / 2;
    }
}
