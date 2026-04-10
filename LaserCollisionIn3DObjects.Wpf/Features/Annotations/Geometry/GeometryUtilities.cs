using System.Windows;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;

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
        var center = new Point(corners.Average(static c => c.X), corners.Average(static c => c.Y));
        var sorted = corners
            .Select(c => new { Point = c, Angle = Math.Atan2(c.Y - center.Y, c.X - center.X) })
            .OrderBy(c => c.Angle)
            .Select(c => c.Point)
            .ToList();

        var topLeftIndex = 0;
        var minScore = double.MaxValue;
        for (var i = 0; i < sorted.Count; i++)
        {
            var score = sorted[i].X + sorted[i].Y;
            if (score < minScore)
            {
                minScore = score;
                topLeftIndex = i;
            }
        }

        return sorted.Skip(topLeftIndex).Concat(sorted.Take(topLeftIndex)).ToArray();
    }
}
