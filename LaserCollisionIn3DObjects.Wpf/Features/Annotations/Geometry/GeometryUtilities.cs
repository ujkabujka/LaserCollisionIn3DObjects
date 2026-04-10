using System.Windows;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;

/// <summary>
/// Shared geometry helpers for annotation processing.
/// </summary>
public static class GeometryUtilities
{
    public static double PolygonArea(IReadOnlyList<Point> points)
    {
        if (points.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            sum += points[i].X * points[j].Y - points[j].X * points[i].Y;
        }

        return Math.Abs(sum) * 0.5;
    }

    public static Point PolygonCentroid(IReadOnlyList<Point> points)
    {
        if (points.Count == 0)
        {
            return new Point(0, 0);
        }

        var signedArea = 0d;
        var cx = 0d;
        var cy = 0d;

        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            var cross = points[i].X * points[j].Y - points[j].X * points[i].Y;
            signedArea += cross;
            cx += (points[i].X + points[j].X) * cross;
            cy += (points[i].Y + points[j].Y) * cross;
        }

        signedArea *= 0.5;
        if (Math.Abs(signedArea) < 1e-8)
        {
            return new Point(points.Average(static p => p.X), points.Average(static p => p.Y));
        }

        var factor = 1.0 / (6.0 * signedArea);
        return new Point(cx * factor, cy * factor);
    }

    public static double CircleArea(double radius) => Math.PI * radius * radius;

    public static double EllipseArea(double rx, double ry) => Math.PI * rx * ry;

    public static IReadOnlyList<Point> OrderCornersTopLeftClockwise(IReadOnlyList<Point> corners)
    {
        if (corners.Count != 4)
        {
            throw new InvalidOperationException("Exactly four corners are required.");
        }

        var sortedByYThenX = corners.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
        var topTwo = sortedByYThenX.Take(2).OrderBy(c => c.X).ToArray();
        var bottomTwo = sortedByYThenX.Skip(2).OrderByDescending(c => c.X).ToArray();

        var ordered = new[] { topTwo[0], topTwo[1], bottomTwo[0], bottomTwo[1] };
        return EnsureClockwise(ordered);
    }

    public static IReadOnlyList<Point> EnsureClockwise(IReadOnlyList<Point> corners)
    {
        if (!IsClockwise(corners))
        {
            return new[] { corners[0], corners[3], corners[2], corners[1] };
        }

        return corners.ToArray();
    }

    public static bool IsClockwise(IReadOnlyList<Point> polygon)
    {
        double sum = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var next = polygon[(i + 1) % polygon.Count];
            sum += (next.X - polygon[i].X) * (next.Y + polygon[i].Y);
        }

        return sum > 0;
    }

    public static bool IsQuadrilateralValid(IReadOnlyList<Point> corners)
    {
        if (corners.Count != 4)
        {
            return false;
        }

        if (PolygonArea(corners) < 1e-3)
        {
            return false;
        }

        return !SegmentsIntersect(corners[0], corners[1], corners[2], corners[3]) &&
               !SegmentsIntersect(corners[1], corners[2], corners[3], corners[0]);
    }

    private static bool SegmentsIntersect(Point p1, Point p2, Point q1, Point q2)
    {
        static double Orientation(Point a, Point b, Point c)
            => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        var o1 = Orientation(p1, p2, q1);
        var o2 = Orientation(p1, p2, q2);
        var o3 = Orientation(q1, q2, p1);
        var o4 = Orientation(q1, q2, p2);

        return (o1 * o2 < 0) && (o3 * o4 < 0);
    }
}
