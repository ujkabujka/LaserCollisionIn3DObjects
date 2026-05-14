using System.Windows;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;

/// <summary>
/// Fits a stable 4-corner rectangle-like quadrilateral from a dense panel contour.
/// </summary>
public sealed class PanelQuadrilateralFitter
{
    public IReadOnlyList<Point> FitFromPolygon(IReadOnlyList<Point> polygon)
    {
        // if (polygon.Count < 3)
        // {
        //     throw new InvalidOperationException("Panel polygon must have at least 3 points.");
        // }

        // var simplified = SimplifyPolygon(polygon);
        // var hull = ComputeConvexHull(simplified);
        // if (hull.Count < 3)
        // {
        //     throw new InvalidOperationException("Panel polygon hull is invalid.");
        // }

        // var bestArea = double.MaxValue;
        // Point[] bestCorners = Array.Empty<Point>();

        // for (var i = 0; i < hull.Count; i++)
        // {
        //     var p0 = hull[i];
        //     var p1 = hull[(i + 1) % hull.Count];
        //     var edge = p1 - p0;
        //     if (edge.Length < 1e-6)
        //     {
        //         continue;
        //     }

        //     var theta = Math.Atan2(edge.Y, edge.X);
        //     var cos = Math.Cos(-theta);
        //     var sin = Math.Sin(-theta);
        //     var rotated = hull.Select(p => RotatePoint(p, cos, sin)).ToList();

        //     var minX = rotated.Min(static p => p.X);
        //     var maxX = rotated.Max(static p => p.X);
        //     var minY = rotated.Min(static p => p.Y);
        //     var maxY = rotated.Max(static p => p.Y);
        //     var width = maxX - minX;
        //     var height = maxY - minY;
        //     var area = width * height;

        //     if (width < 1e-6 || height < 1e-6)
        //     {
        //         continue;
        //     }

        //     if (area < bestArea)
        //     {
        //         bestArea = area;
        //         bestCorners = new[]
        //         {
        //             new Point(minX, minY),
        //             new Point(maxX, minY),
        //             new Point(maxX, maxY),
        //             new Point(minX, maxY),
        //         }.Select(p => RotatePoint(p, Math.Cos(theta), Math.Sin(theta))).ToArray();
        //     }
        // }

        // var ordered = GeometryUtilities.OrderCornersTopLeftClockwise(bestCorners);
        // if (!GeometryUtilities.IsQuadrilateralValid(ordered))
        // {
        //     throw new InvalidOperationException("Fitted panel quadrilateral is degenerate.");
        // }

        // return ordered;

        return FindFourPanelCorners(polygon);
    }

    private static IReadOnlyList<Point> SimplifyPolygon(IReadOnlyList<Point> points)
    {
        if (points.Count <= 4)
        {
            return points;
        }

        var deduped = new List<Point>();
        foreach (var point in points)
        {
            if (deduped.Count == 0 || (point - deduped[^1]).Length > 0.5)
            {
                deduped.Add(point);
            }
        }

        if (deduped.Count > 1 && (deduped[0] - deduped[^1]).Length <= 0.5)
        {
            deduped.RemoveAt(deduped.Count - 1);
        }

        return deduped;
    }

    private static Point RotatePoint(Point p, double cos, double sin)
        => new(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);

    private static List<Point> ComputeConvexHull(IReadOnlyList<Point> points)
    {
        var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        if (pts.Length <= 1)
        {
            return pts.ToList();
        }

        var lower = new List<Point>();
        foreach (var p in pts)
        {
            while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(p);
        }

        var upper = new List<Point>();
        for (var i = pts.Length - 1; i >= 0; i--)
        {
            var p = pts[i];
            while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(p);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static double Cross(Point o, Point a, Point b)
        => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);


    public static List<Point> FindFourPanelCorners(IReadOnlyList<Point> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);

        if (polygon.Count < 4)
            throw new ArgumentException("Polygon must contain at least 4 points.");

        // 1. Optional simplification would help here if your polygon is dense/noisy.
        // For now we use the original polygon directly.
        var pts = RemoveDuplicateClosingPoint(polygon);

        if (pts.Count < 4)
            throw new ArgumentException("Polygon must contain at least 4 distinct points.");

        // 2. Compute corner scores
        var candidates = new List<CornerCandidate>();
        int n = pts.Count;

        for (int i = 0; i < n; i++)
        {
            var prev = pts[(i - 1 + n) % n];
            var curr = pts[i];
            var next = pts[(i + 1) % n];

            var v1 = Normalize(prev - curr);
            var v2 = Normalize(next - curr);

            if (v1.Length < 1e-6 || v2.Length < 1e-6)
                continue;

            double dot = Clamp(v1.X * v2.X + v1.Y * v2.Y, -1.0, 1.0);
            double angle = Math.Acos(dot); // interior angle between outgoing directions

            // Straight line ~ pi radians => low score
            // Strong corner => lower angle => higher score
            double score = Math.PI - angle;

            candidates.Add(new CornerCandidate
            {
                Index = i,
                Point = curr,
                AngleRadians = angle,
                Score = score
            });
        }

        // 3. Take strong candidates first
        candidates = candidates
            .OrderByDescending(c => c.Score)
            .ToList();

        // 4. Pick 4 corners with spacing constraint along contour
        int minIndexSeparation = Math.Max(1, pts.Count / 8);

        var selected = new List<CornerCandidate>();
        foreach (var candidate in candidates)
        {
            bool tooClose = selected.Any(s => CircularIndexDistance(s.Index, candidate.Index, pts.Count) < minIndexSeparation);
            if (tooClose)
                continue;

            selected.Add(candidate);
            if (selected.Count == 4)
                break;
        }

        // 5. Fallback if we did not get 4
        if (selected.Count < 4)
        {
            selected = candidates
                .Take(4)
                .ToList();
        }

        // 6. Order along contour first
        selected = selected
            .OrderBy(c => c.Index)
            .ToList();

        // 7. Convert to consistent image order: TL, TR, BR, BL
        return OrderCornersClockwise(selected.Select(c => c.Point).ToList());
    }

    private static List<Point> RemoveDuplicateClosingPoint(IReadOnlyList<Point> polygon)
    {
        var pts = polygon.ToList();
        if (pts.Count > 1 && Distance(pts[0], pts[^1]) < 1e-6)
            pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    private static int CircularIndexDistance(int a, int b, int count)
    {
        int d = Math.Abs(a - b);
        return Math.Min(d, count - d);
    }

    private static Vector Normalize(Vector v)
    {
        double len = v.Length;
        return len < 1e-12 ? new Vector(0, 0) : v / len;
    }

    private static double Clamp(double x, double min, double max)
        => x < min ? min : (x > max ? max : x);

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static List<Point> OrderCornersClockwise(IReadOnlyList<Point> corners)
    {
        if (corners.Count != 4)
            throw new ArgumentException("Exactly 4 corners are required.");

        var center = new Point(corners.Average(p => p.X), corners.Average(p => p.Y));

        var ordered = corners
            .Select(p => new
            {
                Point = p,
                Angle = Math.Atan2(p.Y - center.Y, p.X - center.X)
            })
            .OrderBy(x => x.Angle)
            .Select(x => x.Point)
            .ToList();

        // Rotate so first point is top-left
        int topLeftIndex = 0;
        double best = double.MaxValue;
        for (int i = 0; i < ordered.Count; i++)
        {
            double score = ordered[i].X + ordered[i].Y;
            if (score < best)
            {
                best = score;
                topLeftIndex = i;
            }
        }

        var rotated = Enumerable.Range(0, 4)
            .Select(i => ordered[(topLeftIndex + i) % 4])
            .ToList();

        // Ensure clockwise order in image coordinates
        if (SignedArea(rotated) < 0)
        {
            rotated = new List<Point>
            {
                rotated[0],
                rotated[3],
                rotated[2],
                rotated[1]
            };
        }

        return rotated;
    }

    private static double SignedArea(IReadOnlyList<Point> pts)
    {
        double area = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area * 0.5;
    }

    private sealed class CornerCandidate
    {
        public int Index { get; set; }
        public Point Point { get; set; } = default;
        public double AngleRadians { get; set; }
        public double Score { get; set; }
    }
}
