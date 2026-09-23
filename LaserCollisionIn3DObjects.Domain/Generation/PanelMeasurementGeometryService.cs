using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Import;

namespace LaserCollisionIn3DObjects.Domain.Generation;

/// <summary>Canonical interpretation of panel-measurement CSV values.</summary>
public static class PanelMeasurementGeometryService
{
    public const double MillimetersPerMeter = 1000d;

    /// <summary>
    /// Preserves the Annotation import's legacy support for distance values above 1000,
    /// which were historically supplied in millimetres despite the column being defined in metres.
    /// </summary>
    public static double NormalizeDistanceMeters(double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0)
            throw new ArgumentException("Panel-corner distance must be finite and greater than zero.", nameof(distance));
        return distance > MillimetersPerMeter ? distance / MillimetersPerMeter : distance;
    }

    public static Vector3 ToWorldPoint(PanelCornerMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        if (!double.IsFinite(measurement.AzimuthDeg) || !double.IsFinite(measurement.ElevationDeg))
            throw new ArgumentException("Panel-corner angles must be finite.", nameof(measurement));

        var distance = (float)NormalizeDistanceMeters(measurement.DistanceMeters);
        var orientation = FrameOrientationBuilder.ApplyLocalZYXulerDegrees(
            Quaternion.Identity, (float)measurement.AzimuthDeg, (float)measurement.ElevationDeg, 0f);
        var point = Vector3.Transform(Vector3.UnitX * distance, orientation);
        if (!IsFinite(point)) throw new ArgumentException("Panel-corner conversion did not produce a finite point.", nameof(measurement));
        return point;
    }

    public static MeasuredPrismGeometry CreatePrismGeometry(
        PanelMeasurementRecord measurement,
        PrismGenerationMethodology methodology = PrismGenerationMethodology.LtAnchoredFourPointBestFit)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        var corners = new[]
        {
            ToWorldPoint(measurement.LeftTop), ToWorldPoint(measurement.RightTop),
            ToWorldPoint(measurement.RightBottom), ToWorldPoint(measurement.LeftBottom),
        };
        var size = new Vector3(
            (float)(measurement.ThicknessMm * .001),
            (float)(measurement.WidthMm * .001),
            (float)(measurement.HeightMm * .001));
        if (!IsFinite(size) || size.X <= 0 || size.Y <= 0 || size.Z <= 0)
            throw new ArgumentException("Measured prism dimensions must be finite and greater than zero.", nameof(measurement));

        var frame = MeasuredPanelFrameBuilder.Create(
            corners[0], corners[1], corners[2], corners[3], size.Y, size.Z, methodology);
        // Keep the measured/reference plane at the prism center plane, matching Annotation generation.
        var center = corners[0] + frame.Width * size.Y / 2f + frame.Down * size.Z / 2f;
        return new MeasuredPrismGeometry(center, size, frame.Orientation, corners, frame.Residuals);
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public sealed record MeasuredPrismGeometry(
    Vector3 Position,
    Vector3 Size,
    Quaternion Orientation,
    IReadOnlyList<Vector3> MeasuredCorners,
    PanelFitResiduals? Residuals);
