using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;

public sealed class OverlayRenderer
{
    private static readonly Brush PanelPolygonBrush = Brushes.LimeGreen;
    private static readonly Brush PanelFitBrush = Brushes.Orange;
    private static readonly Brush HoleBrush = Brushes.DeepSkyBlue;
    private static readonly Brush HoleFillBrush = new SolidColorBrush(Color.FromArgb(180, 0, 191, 255));

    public BitmapSource CreateOriginalOverlay(AnnotatedImageRecord record, BitmapSource image)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        dc.DrawImage(image, new Rect(0, 0, image.PixelWidth, image.PixelHeight));

        if (record.Panel is not null)
        {
            var panelGeometry = BuildPolygon(record.Panel.OriginalPolygonPoints);
            dc.DrawGeometry(null, new Pen(PanelPolygonBrush, 2.5), panelGeometry);

            if (record.Panel.FittedQuadrilateralCorners.Count == 4)
            {
                var fit = BuildPolygon(record.Panel.FittedQuadrilateralCorners);
                dc.DrawGeometry(null, new Pen(PanelFitBrush, 3.5), fit);

                for (var i = 0; i < 4; i++)
                {
                    var corner = record.Panel.FittedQuadrilateralCorners[i];
                    DrawPointWithLabel(dc, corner, $"C{i + 1}", Brushes.OrangeRed, Brushes.Gold, 5);
                }
            }
        }

        for (var i = 0; i < record.Holes.Count; i++)
        {
            DrawHoleOutline(dc, record.Holes[i].OriginalShape);
            DrawPointWithLabel(dc, record.Holes[i].CenterPoint, $"H{i + 1}", HoleBrush, HoleFillBrush, 5, 12);
        }

        return RenderVisual(visual, image.PixelWidth, image.PixelHeight);
    }

    public BitmapSource CreateWarpedOverlay(RectificationResult rectification)
    {
        var width = (int)rectification.DestinationSizePixels.Width;
        var height = (int)rectification.DestinationSizePixels.Height;
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        dc.DrawImage(rectification.WarpedImage, new Rect(0, 0, width, height));

        if (rectification.OrderedDestinationCorners.Count >= 4)
        {
            var panelPolygon = BuildPolygon(rectification.OrderedDestinationCorners);
            dc.DrawGeometry(null, new Pen(PanelFitBrush, 3.5), panelPolygon);

            for (var i = 0; i < 4; i++)
            {
                var corner = rectification.OrderedDestinationCorners[i];
                DrawPointWithLabel(dc, corner, $"R{i + 1}", Brushes.OrangeRed, Brushes.Gold, 5);
            }
        }

        for (var i = 0; i < rectification.TransformedHoleCenters.Count; i++)
        {
            DrawPointWithLabel(dc, rectification.TransformedHoleCenters[i], $"H{i + 1}", HoleBrush, HoleFillBrush, 6, 12);
        }

        return RenderVisual(visual, width, height);
    }

    private static void DrawHoleOutline(DrawingContext dc, IAnnotationShape shape)
    {
        var pen = new Pen(HoleBrush, 2) { DashStyle = DashStyles.Dash };
        switch (shape)
        {
            case PolygonShapeData polygon when polygon.Points.Count >= 3:
                dc.DrawGeometry(null, pen, BuildPolygon(polygon.Points));
                break;
            case CircleShapeData circle:
                dc.DrawEllipse(null, pen, circle.Center, circle.Radius, circle.Radius);
                break;
            case EllipseShapeData ellipse:
                dc.DrawEllipse(null, pen, ellipse.Center, ellipse.RadiusX, ellipse.RadiusY);
                break;
        }
    }

    private static void DrawPointWithLabel(DrawingContext dc, Point point, string label, Brush stroke, Brush fill, double radius, double fontSize = 13)
    {
        dc.DrawEllipse(fill, new Pen(stroke, 1.6), point, radius, radius);
        var text = new FormattedText(
            label,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            stroke,
            1.0);
        dc.DrawText(text, new Point(point.X + radius + 3, point.Y + radius + 2));
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
