using System.Windows;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Rectification;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;

public sealed class AnnotationWorkspaceService
{
    private readonly ViaAnnotationLoader _loader = new();
    private readonly PanelQuadrilateralFitter _fitter = new();
    private readonly PanelRectificationService _rectifier = new();
    private readonly OverlayRenderer _overlayRenderer = new();

    public AnnotationProject LoadProject(string folderPath) => _loader.LoadFromFolder(folderPath);

    public BitmapSource LoadImage(string imagePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void FitPanel(AnnotatedImageRecord record)
    {
        if (record.Panel is null)
        {
            return;
        }

        record.Panel.FittedQuadrilateralCorners = _fitter.FitFromPolygon(record.Panel.OriginalPolygonPoints);
    }

    public RectificationResult? CreateRectification(AnnotatedImageRecord record, BitmapSource image)
    {
        if (record.Panel?.FittedQuadrilateralCorners.Count != 4)
        {
            return null;
        }

        var ordered = GeometryUtilities.OrderCornersTopLeftClockwise(record.Panel.FittedQuadrilateralCorners);
        return _rectifier.Rectify(image, ordered, record.Holes.Select(static h => h.CenterPoint).ToArray());
    }

    public BitmapSource CreateOriginalOverlay(AnnotatedImageRecord record, BitmapSource image)
        => _overlayRenderer.CreateOriginalOverlay(record, image);

    public BitmapSource CreateWarpedOverlay(RectificationResult rectification, BitmapSource warpedImage)
        => _overlayRenderer.CreateWarpedOverlay(rectification, warpedImage);

    public static IReadOnlyList<HoleViewModel> BuildHoleRows(
        AnnotatedImageRecord record,
        RectificationResult? rectification,
        PanelMetricCalibration calibration)
    {
        var rows = new List<HoleViewModel>(record.Holes.Count);
        var canConvertToMm = rectification is not null
            && calibration.IsConfigured
            && rectification.DestinationSizePixels.Width > 0
            && rectification.DestinationSizePixels.Height > 0;

        var mmScaleX = canConvertToMm ? calibration.PhysicalWidthMm!.Value / rectification!.DestinationSizePixels.Width : 0d;
        var mmScaleY = canConvertToMm ? calibration.PhysicalHeightMm!.Value / rectification!.DestinationSizePixels.Height : 0d;

        for (var i = 0; i < record.Holes.Count; i++)
        {
            var hole = record.Holes[i];
            var warpedCenter = rectification?.TransformedHoleCenters.ElementAtOrDefault(i) ?? new Point(double.NaN, double.NaN);
            var center = new Point(warpedCenter.X * mmScaleX, warpedCenter.Y * mmScaleY);
            var warpedCenterMm = canConvertToMm && !double.IsNaN(warpedCenter.X)
                ? $"({(center.X):F2}, {(center.Y):F2})"
                : "N/A";
            rows.Add(new HoleViewModel
            {
                Index = i + 1,
                ShapeType = hole.ShapeType,
                OriginalCenter = $"({hole.CenterPoint.X:F1}, {hole.CenterPoint.Y:F1})",
                WarpedCenter = double.IsNaN(warpedCenter.X) ? "N/A" : $"({warpedCenter.X:F1}, {warpedCenter.Y:F1})",
                WarpedCenterMm = warpedCenterMm,
                WarpedCenterMmNumeric = center,
                PixelArea = hole.PixelArea.ToString("F2"),
            });
        }

        return rows;
    }
}
