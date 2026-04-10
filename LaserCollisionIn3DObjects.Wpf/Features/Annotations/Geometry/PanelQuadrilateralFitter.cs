using System.Windows;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;

/// <summary>
/// Fits a stable 4-corner rectangle-like quadrilateral from a dense panel contour.
/// </summary>
public sealed class PanelQuadrilateralFitter
{
    public IReadOnlyList<Point> FitFromPolygon(IReadOnlyList<Point> polygon)
    {
        if (polygon.Count < 3)
        {
            throw new InvalidOperationException("Panel polygon must have at least 3 points.");
        }

        var hull = ComputeConvexHull(polygon);
        if (hull.Count < 3)
        {
            throw new InvalidOperationException("Panel polygon hull is invalid.");
        }

        var bestArea = double.MaxValue;
        Point[] bestCorners = Array.Empty<Point>();

        for (var i = 0; i < hull.Count; i++)
        {
            var p0 = hull[i];
            var p1 = hull[(i + 1) % hull.Count];
            var edge = p1 - p0;
            var theta = Math.Atan2(edge.Y, edge.X);

            var cos = Math.Cos(-theta);
            var sin = Math.Sin(-theta);

            var rotated = hull.Select(p => RotatePoint(p, cos, sin)).ToList();
            var minX = rotated.Min(static p => p.X);
            var maxX = rotated.Max(static p => p.X);
            var minY = rotated.Min(static p => p.Y);
            var maxY = rotated.Max(static p => p.Y);
            var area = (maxX - minX) * (maxY - minY);

            if (area < bestArea)
            {
                bestArea = area;
                var candidate = new[]
                {
                    new Point(minX, minY),
                    new Point(maxX, minY),
                    new Point(maxX, maxY),
                    new Point(minX, maxY),
                }.Select(p => RotatePoint(p, Math.Cos(theta), Math.Sin(theta))).ToArray();
                bestCorners = candidate;
            }
        }

        return GeometryUtilities.OrderCornersTopLeftClockwise(bestCorners);
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
}
