using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Rectification;

/// <summary>
/// Generates a fronto-parallel panel view and transforms hole coordinates.
/// </summary>
public sealed class PanelRectificationService
{
    public RectificationResult Rectify(BitmapSource sourceImage, IReadOnlyList<Point> sourceCorners, IReadOnlyList<Point> holeCenters)
    {
        var orderedSource = PrepareSourceCorners(sourceCorners);
        var destinationCorners = BuildDestinationCorners(orderedSource, out var width, out var height);

        var srcToDst = Homography.ComputeFromFourPointPairs(orderedSource, destinationCorners);
        var dstToSrc = Homography.Invert3x3(srcToDst);

        var warped = WarpImage(sourceImage, width, height, dstToSrc);
        var transformedHoleCenters = holeCenters.Select(p => Homography.Transform(p, srcToDst)).ToArray();

        return new RectificationResult
        {
            OrderedSourceCorners = orderedSource,
            OrderedDestinationCorners = destinationCorners,
            DestinationSizePixels = new Size(width, height),
            SourceToDestinationHomography = srcToDst,
            WarpedImage = warped,
            TransformedHoleCenters = transformedHoleCenters,
        };
    }

    private static IReadOnlyList<Point> PrepareSourceCorners(IReadOnlyList<Point> sourceCorners)
    {
        if (sourceCorners.Count != 4)
        {
            throw new InvalidOperationException("Panel rectification requires exactly four source corners.");
        }

        var ordered = GeometryUtilities.OrderCornersTopLeftClockwise(sourceCorners);
        if (!GeometryUtilities.IsQuadrilateralValid(ordered))
        {
            throw new InvalidOperationException("Panel corners are degenerate or self-intersecting.");
        }

        return ordered;
    }

    private static IReadOnlyList<Point> BuildDestinationCorners(IReadOnlyList<Point> orderedSource, out int targetWidth, out int targetHeight)
    {
        var topWidth = Distance(orderedSource[0], orderedSource[1]);
        var bottomWidth = Distance(orderedSource[3], orderedSource[2]);
        var leftHeight = Distance(orderedSource[0], orderedSource[3]);
        var rightHeight = Distance(orderedSource[1], orderedSource[2]);

        targetWidth = Math.Max(16, (int)Math.Round(Math.Max(topWidth, bottomWidth)));
        targetHeight = Math.Max(16, (int)Math.Round(Math.Max(leftHeight, rightHeight)));

        return
        [
            new Point(0, 0),
            new Point(targetWidth - 1, 0),
            new Point(targetWidth - 1, targetHeight - 1),
            new Point(0, targetHeight - 1),
        ];
    }

    private static WriteableBitmap WarpImage(BitmapSource source, int width, int height, double[] destinationToSourceHomography)
    {
        var formatted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var sourceStride = formatted.PixelWidth * 4;
        var sourceBuffer = new byte[sourceStride * formatted.PixelHeight];
        formatted.CopyPixels(sourceBuffer, sourceStride, 0);

        var destinationStride = width * 4;
        var destinationBuffer = new byte[destinationStride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sourcePoint = Homography.Transform(new Point(x, y), destinationToSourceHomography);
                var sample = SampleBilinear(sourceBuffer, sourceStride, formatted.PixelWidth, formatted.PixelHeight, sourcePoint.X, sourcePoint.Y);
                var index = (y * destinationStride) + (x * 4);
                destinationBuffer[index + 0] = sample.B;
                destinationBuffer[index + 1] = sample.G;
                destinationBuffer[index + 2] = sample.R;
                destinationBuffer[index + 3] = 255;
            }
        }

        var bitmap = new WriteableBitmap(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), destinationBuffer, destinationStride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static (byte B, byte G, byte R) SampleBilinear(byte[] pixels, int stride, int width, int height, double x, double y)
    {
        if (x < 0 || y < 0 || x >= width - 1 || y >= height - 1)
        {
            return (0, 0, 0);
        }

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        var tx = x - x0;
        var ty = y - y0;

        static (byte B, byte G, byte R) Read(byte[] buffer, int str, int px, int py)
        {
            var idx = (py * str) + (px * 4);
            return (buffer[idx + 0], buffer[idx + 1], buffer[idx + 2]);
        }

        var c00 = Read(pixels, stride, x0, y0);
        var c10 = Read(pixels, stride, x1, y0);
        var c01 = Read(pixels, stride, x0, y1);
        var c11 = Read(pixels, stride, x1, y1);

        static byte Interpolate(byte a, byte b, byte c, byte d, double tx, double ty)
        {
            var top = a + ((b - a) * tx);
            var bottom = c + ((d - c) * tx);
            return (byte)Math.Clamp(top + ((bottom - top) * ty), 0, 255);
        }

        return (
            Interpolate(c00.B, c10.B, c01.B, c11.B, tx, ty),
            Interpolate(c00.G, c10.G, c01.G, c11.G, tx, ty),
            Interpolate(c00.R, c10.R, c01.R, c11.R, tx, ty));
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
