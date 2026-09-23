using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Import;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class PanelMeasurementGeometryServiceTests
{
    [Theory]
    [InlineData(10, 0, 0, 10, 0, 0)]
    [InlineData(10, 90, 0, 0, 10, 0)]
    [InlineData(10, 0, 30, 8.660254, 0, -5)]
    [InlineData(10, 45, -30, 6.123724, 6.123724, 5)]
    public void ToWorldPoint_PreservesAnnotationAngleConvention(
        double distance, double azimuth, double elevation, double x, double y, double z)
    {
        var actual = PanelMeasurementGeometryService.ToWorldPoint(new(distance, azimuth, elevation));

        AssertVector(new Vector3((float)x, (float)y, (float)z), actual, 1e-5f);
    }

    [Fact]
    public void ToWorldPoint_PreservesLegacyMillimetreLikeDistanceCompatibility()
    {
        AssertVector(new Vector3(10, 0, 0),
            PanelMeasurementGeometryService.ToWorldPoint(new(10_000, 0, 0)), 1e-5f);
    }

    [Fact]
    public void ToWorldPoint_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => PanelMeasurementGeometryService.ToWorldPoint(new(double.NaN, 0, 0)));
        Assert.Throws<ArgumentException>(() => PanelMeasurementGeometryService.ToWorldPoint(new(1, double.PositiveInfinity, 0)));
    }

    [Fact]
    public void CreatePrismGeometry_UsesFourPointFitAndAnnotationDimensionMapping()
    {
        var record = RectangleAtPositiveX();

        var geometry = PanelMeasurementGeometryService.CreatePrismGeometry(record);

        AssertVector(new Vector3(.01f, 2f, 3f), geometry.Size, 1e-6f);
        AssertVector(new Vector3(10f, 1f, -1.5f), geometry.Position, 1e-4f);
        Assert.Equal(4, geometry.MeasuredCorners.Count);
        Assert.True(float.IsFinite(geometry.Orientation.X));
        Assert.InRange(geometry.Orientation.Length(), .9999f, 1.0001f);
        Assert.NotNull(geometry.Residuals);
        Assert.True(float.IsFinite(geometry.Residuals!.Value.Rmse));
    }

    [Fact]
    public void DirectCollisionAndAnnotationGeneration_AreGeometricallyEquivalent()
    {
        var record = RectangleAtPositiveX();
        var direct = PanelMeasurementGeometryService.CreatePrismGeometry(record);
        var annotationCorners = new[] { record.LeftTop, record.RightTop, record.RightBottom, record.LeftBottom }
            .Select(PanelMeasurementGeometryService.ToWorldPoint).ToArray();
        var annotationSize = new Vector3(
            (float)record.ThicknessMm * .001f,
            (float)record.WidthMm * .001f,
            (float)record.HeightMm * .001f);
        var annotationFrame = MeasuredPanelFrameBuilder.Create(
            annotationCorners[0], annotationCorners[1], annotationCorners[2], annotationCorners[3],
            annotationSize.Y, annotationSize.Z, PrismGenerationMethodology.LtAnchoredFourPointBestFit);
        var annotationCenter = annotationCorners[0]
            + annotationFrame.Width * annotationSize.Y / 2f
            + annotationFrame.Down * annotationSize.Z / 2f;

        AssertVector(annotationSize, direct.Size, 1e-6f);
        AssertVector(annotationCenter, direct.Position, 1e-6f);
        Assert.InRange(MathF.Abs(Quaternion.Dot(annotationFrame.Orientation, direct.Orientation)), .99999f, 1f);
        for (var i = 0; i < annotationCorners.Length; i++) AssertVector(annotationCorners[i], direct.MeasuredCorners[i], 1e-6f);
    }

    private static PanelMeasurementRecord RectangleAtPositiveX() => new(
        2000, 3000, 10,
        Spherical(new(10, 0, 0)), Spherical(new(10, 2, 0)),
        Spherical(new(10, 2, -3)), Spherical(new(10, 0, -3)));

    private static PanelCornerMeasurement Spherical(Vector3 point)
    {
        var radius = point.Length();
        return new(radius,
            Math.Atan2(point.Y, point.X) * 180 / Math.PI,
            Math.Atan2(-point.Z, Math.Sqrt(point.X * point.X + point.Y * point.Y)) * 180 / Math.PI);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0, tolerance);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0, tolerance);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0, tolerance);
    }
}
