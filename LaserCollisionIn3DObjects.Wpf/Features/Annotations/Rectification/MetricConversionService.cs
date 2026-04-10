using System.Windows;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Rectification;

/// <summary>
/// Placeholder service for future metric conversion once panel physical dimensions are known.
/// </summary>
public sealed class MetricConversionService
{
    public Point ConvertWarpedPixelToMillimeters(Point warpedPoint, RectificationResult rectification, PanelMetricCalibration calibration)
    {
        if (!calibration.IsConfigured)
        {
            throw new InvalidOperationException("Panel metric calibration is not configured.");
        }

        var mmPerPixelX = calibration.PhysicalWidthMm!.Value / rectification.DestinationSizePixels.Width;
        var mmPerPixelY = calibration.PhysicalHeightMm!.Value / rectification.DestinationSizePixels.Height;
        return new Point(warpedPoint.X * mmPerPixelX, warpedPoint.Y * mmPerPixelY);
    }

    public double ConvertPixelAreaToSquareMillimeters(double pixelArea, RectificationResult rectification, PanelMetricCalibration calibration)
    {
        if (!calibration.IsConfigured)
        {
            throw new InvalidOperationException("Panel metric calibration is not configured.");
        }

        var mmPerPixelX = calibration.PhysicalWidthMm!.Value / rectification.DestinationSizePixels.Width;
        var mmPerPixelY = calibration.PhysicalHeightMm!.Value / rectification.DestinationSizePixels.Height;
        return pixelArea * mmPerPixelX * mmPerPixelY;
    }
}
