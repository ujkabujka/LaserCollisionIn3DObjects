namespace LaserCollisionIn3DObjects.Domain.Imaging;

/// <summary>A two-dimensional image coordinate independent of a UI framework.</summary>
public readonly record struct ImagePoint(double X, double Y);

/// <summary>Finds four distinct physical bends in an approximately quadrilateral contour.</summary>
public static class PanelCornerDetector
{
    // A bend occupying less than 3% of the boundary is treated as one physical corner.
    // Unlike a vertex-count threshold, this remains meaningful for uneven VIA sampling.
    private const double CornerSuppressionArcFraction = 0.03;
    private const int MaximumCandidateCount = 24;
    private const double MinimumAreaCoverage = 0.50;
    private const double MinimumEdgePerimeterFraction = 0.04;

    public static IReadOnlyList<ImagePoint> Fit(IReadOnlyList<ImagePoint> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var points = RemoveConsecutiveDuplicates(polygon);
        if (points.Count < 4)
            throw new ArgumentException("Panel polygon must contain at least four distinct points.", nameof(polygon));

        var contourArea = Area(points);
        var cumulative = ComputeCumulativeArcLengths(points, out var perimeter);
        if (contourArea <= 1e-6 || perimeter <= 1e-6)
            throw new InvalidOperationException("Panel polygon has no meaningful area.");

        var candidates = ComputeCandidates(points)
            .OrderByDescending(static candidate => candidate.Strength)
            .ToList();
        var distinct = SuppressNearbyCandidates(candidates, cumulative, perimeter);

        // Ensure that a quiet (slightly rounded) extreme is not lost when noisy edge points
        // fill the angle-ranked pool.
        foreach (var extreme in GetDirectionalExtremes(candidates))
        {
            AddIfPhysicallyDistinct(distinct, extreme, cumulative, perimeter);
        }

        if (distinct.Count < 4)
            throw new InvalidOperationException("Panel contour does not contain four distinct physical corners.");

        Candidate[]? best = null;
        var bestScore = double.NegativeInfinity;
        for (var a = 0; a < distinct.Count - 3; a++)
            for (var b = a + 1; b < distinct.Count - 2; b++)
                for (var c = b + 1; c < distinct.Count - 1; c++)
                    for (var d = c + 1; d < distinct.Count; d++)
                    {
                        var combination = new[] { distinct[a], distinct[b], distinct[c], distinct[d] };
                        var ordered = OrderTopLeftClockwise(combination.Select(static item => item.Point).ToArray());
                        if (!IsMeaningfulQuadrilateral(ordered, contourArea, perimeter, out var coverage))
                            continue;

                        // Coverage strongly rejects the old triangle-like fallback, while angle strength
                        // distinguishes similarly sized quadrilaterals without requiring right angles.
                        var coverageQuality = Math.Exp(-Math.Abs(Math.Log(coverage)));
                        var score = (10 * coverageQuality) + combination.Sum(static item => item.Strength);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = combination;
                        }
                    }

        if (best is null)
            throw new InvalidOperationException("Panel contour cannot produce a meaningful four-corner quadrilateral.");

        return OrderTopLeftClockwise(best.Select(static item => item.Point).ToArray());
    }

    private static List<Candidate> ComputeCandidates(IReadOnlyList<ImagePoint> points)
    {
        var result = new List<Candidate>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var previous = points[(index - 1 + points.Count) % points.Count];
            var current = points[index];
            var next = points[(index + 1) % points.Count];
            var first = Normalize(previous, current);
            var second = Normalize(next, current);
            if (first.Length <= 1e-12 || second.Length <= 1e-12)
                continue;

            var angle = Math.Acos(Math.Clamp((first.X * second.X) + (first.Y * second.Y), -1, 1));
            result.Add(new Candidate(index, current, Math.PI - angle));
        }
        return result;
    }

    private static List<Candidate> SuppressNearbyCandidates(
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<double> cumulative,
        double perimeter)
    {
        var selected = new List<Candidate>();
        foreach (var candidate in candidates)
        {
            AddIfPhysicallyDistinct(selected, candidate, cumulative, perimeter);
            if (selected.Count == MaximumCandidateCount)
                break;
        }
        return selected;
    }

    private static void AddIfPhysicallyDistinct(
        ICollection<Candidate> selected,
        Candidate candidate,
        IReadOnlyList<double> cumulative,
        double perimeter)
    {
        var minimumArc = perimeter * CornerSuppressionArcFraction;
        if (selected.Any(existing => CircularArcDistance(existing.Index, candidate.Index, cumulative, perimeter) < minimumArc))
            return;
        selected.Add(candidate);
    }

    private static IEnumerable<Candidate> GetDirectionalExtremes(IReadOnlyList<Candidate> candidates)
    {
        yield return candidates.MinBy(static item => item.Point.X + item.Point.Y)!;
        yield return candidates.MaxBy(static item => item.Point.X - item.Point.Y)!;
        yield return candidates.MaxBy(static item => item.Point.X + item.Point.Y)!;
        yield return candidates.MinBy(static item => item.Point.X - item.Point.Y)!;
    }

    private static double[] ComputeCumulativeArcLengths(IReadOnlyList<ImagePoint> points, out double perimeter)
    {
        var cumulative = new double[points.Count];
        for (var index = 1; index < points.Count; index++)
            cumulative[index] = cumulative[index - 1] + Distance(points[index - 1], points[index]);
        perimeter = cumulative[^1] + Distance(points[^1], points[0]);
        return cumulative;
    }

    private static double CircularArcDistance(int first, int second, IReadOnlyList<double> cumulative, double perimeter)
    {
        var forward = Math.Abs(cumulative[first] - cumulative[second]);
        return Math.Min(forward, perimeter - forward);
    }

    private static bool IsMeaningfulQuadrilateral(
        IReadOnlyList<ImagePoint> corners,
        double contourArea,
        double perimeter,
        out double coverage)
    {
        var quadrilateralArea = Area(corners);
        coverage = quadrilateralArea / contourArea;
        if (coverage < MinimumAreaCoverage)
            return false;

        var minimumEdge = perimeter * MinimumEdgePerimeterFraction;
        for (var index = 0; index < 4; index++)
        {
            if (Distance(corners[index], corners[(index + 1) % 4]) < minimumEdge)
                return false;
        }

        double? turnSign = null;
        for (var index = 0; index < 4; index++)
        {
            var cross = Cross(corners[index], corners[(index + 1) % 4], corners[(index + 2) % 4]);
            if (Math.Abs(cross) <= 1e-8)
                return false;
            turnSign ??= Math.Sign(cross);
            if (Math.Sign(cross) != turnSign)
                return false;
        }
        return true;
    }

    private static IReadOnlyList<ImagePoint> OrderTopLeftClockwise(IReadOnlyList<ImagePoint> corners)
    {
        var center = new ImagePoint(corners.Average(static point => point.X), corners.Average(static point => point.Y));
        var circular = corners.OrderBy(point => Math.Atan2(point.Y - center.Y, point.X - center.X)).ToList();
        var topLeft = circular.Select((point, index) => (point, index))
            .MinBy(static item => item.point.X + item.point.Y).index;
        var ordered = Enumerable.Range(0, 4).Select(offset => circular[(topLeft + offset) % 4]).ToArray();
        if (SignedArea(ordered) < 0)
            ordered = new[] { ordered[0], ordered[3], ordered[2], ordered[1] };
        return ordered;
    }

    private static List<ImagePoint> RemoveConsecutiveDuplicates(IReadOnlyList<ImagePoint> points)
    {
        var result = new List<ImagePoint>();
        foreach (var point in points)
            if (result.Count == 0 || Distance(result[^1], point) > 1e-6)
                result.Add(point);
        if (result.Count > 1 && Distance(result[0], result[^1]) <= 1e-6)
            result.RemoveAt(result.Count - 1);
        return result;
    }

    private static (double X, double Y, double Length) Normalize(ImagePoint point, ImagePoint origin)
    {
        var x = point.X - origin.X;
        var y = point.Y - origin.Y;
        var length = Math.Sqrt((x * x) + (y * y));
        return length <= 1e-12 ? (0, 0, 0) : (x / length, y / length, length);
    }

    private static double Area(IReadOnlyList<ImagePoint> points) => Math.Abs(SignedArea(points));
    private static double SignedArea(IReadOnlyList<ImagePoint> points)
    {
        var sum = 0d;
        for (var index = 0; index < points.Count; index++)
            sum += (points[index].X * points[(index + 1) % points.Count].Y) - (points[(index + 1) % points.Count].X * points[index].Y);
        return sum / 2;
    }

    private static double Cross(ImagePoint origin, ImagePoint first, ImagePoint second)
        => ((first.X - origin.X) * (second.Y - origin.Y)) - ((first.Y - origin.Y) * (second.X - origin.X));
    private static double Distance(ImagePoint first, ImagePoint second)
        => Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));

    private sealed record Candidate(int Index, ImagePoint Point, double Strength);
}
