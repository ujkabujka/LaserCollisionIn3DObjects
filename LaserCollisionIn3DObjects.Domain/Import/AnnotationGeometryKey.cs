using System.Globalization;

namespace LaserCollisionIn3DObjects.Domain.Import;

/// <summary>
/// Exact semantic/source-geometry identity for a VIA point annotation.
/// </summary>
public readonly record struct AnnotationGeometryKey(
    AnnotationSemanticType Category,
    AnnotationGeometryShape Shape,
    string CanonicalGeometry)
{
    public static AnnotationGeometryKey Circle(AnnotationSemanticType category, double x, double y, double radius)
        => new(category, AnnotationGeometryShape.Circle, Join(x, y, radius));

    public static AnnotationGeometryKey Ellipse(AnnotationSemanticType category, double x, double y, double radiusX, double radiusY)
        => new(category, AnnotationGeometryShape.Ellipse, Join(x, y, radiusX, radiusY));

    public static AnnotationGeometryKey Polygon(AnnotationSemanticType category, IEnumerable<AnnotationVertex> vertices)
    {
        var points = vertices.ToList();
        if (points.Count > 1 && points[0] == points[^1]) points.RemoveAt(points.Count - 1);
        if (points.Count == 0) return new(category, AnnotationGeometryShape.Polygon, string.Empty);

        string? canonical = null;
        for (var start = 0; start < points.Count; start++)
        {
            Consider(BuildRotation(points, start, 1));
            Consider(BuildRotation(points, start, -1));
        }

        return new(category, AnnotationGeometryShape.Polygon, canonical!);

        void Consider(string candidate)
        {
            if (canonical is null || string.CompareOrdinal(candidate, canonical) < 0) canonical = candidate;
        }
    }

    private static string BuildRotation(IReadOnlyList<AnnotationVertex> points, int start, int direction)
    {
        var values = new string[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var index = (start + direction * i) % points.Count;
            if (index < 0) index += points.Count;
            values[i] = Join(points[index].X, points[index].Y);
        }
        return string.Join(';', values);
    }

    private static string Join(params double[] values) => string.Join(',', values.Select(Exact));

    // "R" preserves parsed double identity without introducing rounding; normalize signed zero
    // because exact numeric equality considers +0 and -0 equal.
    private static string Exact(double value) => (value == 0d ? 0d : value).ToString("R", CultureInfo.InvariantCulture);
}

public enum AnnotationGeometryShape { Polygon, Circle, Ellipse }

public readonly record struct AnnotationVertex(double X, double Y);
