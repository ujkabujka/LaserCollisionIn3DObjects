using System.Windows;
using System.Windows.Media.Imaging;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Rectification;

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
        => _overlayRenderer.CreateOriginalOverlay(record, image.PixelWidth, image.PixelHeight);

    public BitmapSource CreateWarpedOverlay(RectificationResult rectification)
        => _overlayRenderer.CreateWarpedOverlay(rectification.TransformedHoleCenters, rectification.WarpedImage.PixelWidth, rectification.WarpedImage.PixelHeight);
}
