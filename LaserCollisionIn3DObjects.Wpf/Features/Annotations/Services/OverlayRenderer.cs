using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;

public sealed class OverlayRenderer
{
    public BitmapSource CreateOriginalOverlay(AnnotatedImageRecord record, int width, int height)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();

        if (record.Panel is not null)
        {
            var panelGeometry = BuildPolygon(record.Panel.OriginalPolygonPoints);
            dc.DrawGeometry(null, new Pen(Brushes.LimeGreen, 2), panelGeometry);

            if (record.Panel.FittedQuadrilateralCorners.Count == 4)
            {
                var fit = BuildPolygon(record.Panel.FittedQuadrilateralCorners);
                dc.DrawGeometry(null, new Pen(Brushes.Orange, 2.5), fit);

                for (var i = 0; i < 4; i++)
                {
                    var corner = record.Panel.FittedQuadrilateralCorners[i];
                    dc.DrawEllipse(Brushes.OrangeRed, null, corner, 4, 4);
                    var label = new FormattedText(
                        $"C{i + 1}",
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        14,
                        Brushes.OrangeRed,
                        1.0);
                    dc.DrawText(label, new Point(corner.X + 5, corner.Y + 4));
                }
            }
        }

        foreach (var hole in record.Holes)
        {
            dc.DrawEllipse(Brushes.DeepSkyBlue, null, hole.CenterPoint, 3.5, 3.5);
        }

        return RenderVisual(visual, width, height);
    }

    public BitmapSource CreateWarpedOverlay(IReadOnlyList<Point> transformedHoleCenters, int width, int height)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();

        dc.DrawRectangle(null, new Pen(Brushes.Orange, 2), new Rect(1, 1, Math.Max(1, width - 2), Math.Max(1, height - 2)));
        foreach (var point in transformedHoleCenters)
        {
            dc.DrawEllipse(Brushes.DeepSkyBlue, null, point, 3.5, 3.5);
        }

        return RenderVisual(visual, width, height);
    }

    private static StreamGeometry BuildPolygon(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], false, true);
        ctx.PolyLineTo(points.Skip(1).ToArray(), true, true);
        geometry.Freeze();
        return geometry;
    }

    private static BitmapSource RenderVisual(Visual visual, int width, int height)
    {
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }
}
