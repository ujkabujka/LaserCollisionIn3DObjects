using System.Windows;
using LaserCollisionIn3DObjects.Domain.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;

/// <summary>Fits the four photographed panel corners from an annotated contour.</summary>
public sealed class PanelQuadrilateralFitter
{
    public IReadOnlyList<Point> FitFromPolygon(IReadOnlyList<Point> polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);

        var fitted = PanelCornerDetector.Fit(
            polygon.Select(static point => new ImagePoint(point.X, point.Y)).ToArray());
        var ordered = fitted.Select(static point => new Point(point.X, point.Y)).ToArray();

        if (!GeometryUtilities.IsQuadrilateralValid(ordered))
        {
            throw new InvalidOperationException("Fitted panel quadrilateral is degenerate or self-intersecting.");
        }

        return ordered;
    }

    public static List<Point> FindFourPanelCorners(IReadOnlyList<Point> polygon)
        => new PanelQuadrilateralFitter().FitFromPolygon(polygon).ToList();

    public static List<Point> OrderCornersClockwise(IReadOnlyList<Point> corners)
        => GeometryUtilities.OrderCornersTopLeftClockwise(corners).ToList();
}
