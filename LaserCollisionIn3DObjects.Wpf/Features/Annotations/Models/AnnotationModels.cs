using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

public enum AnnotationShapeType
{
    Polygon,
    Circle,
    Ellipse,
}

public sealed class AnnotationProject
{
    public required string JsonFilePath { get; init; }

    public List<AnnotatedImageRecord> Images { get; } = new();

    public List<string> Diagnostics { get; } = new();
}

public sealed class AnnotatedImageRecord
{
    public required string Key { get; init; }

    public required string FileName { get; init; }

    public string? ImagePath { get; set; }

    public PanelAnnotation? Panel { get; set; }

    public List<HoleAnnotation> Holes { get; } = new();

    public List<string> Diagnostics { get; } = new();

    public PanelMetricCalibration Calibration { get; set; } = new();

    public bool HasSinglePanel => Panel is not null;

    public bool IsImageMissing => string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath);
}

public sealed class PanelAnnotation
{
    public required IReadOnlyList<Point> OriginalPolygonPoints { get; init; }

    public IReadOnlyList<Point> FittedQuadrilateralCorners { get; set; } = Array.Empty<Point>();
}

public sealed class HoleAnnotation
{
    public required AnnotationShapeType ShapeType { get; init; }

    public required IAnnotationShape OriginalShape { get; init; }

    public required Point CenterPoint { get; init; }

    public required double PixelArea { get; init; }
}

public interface IAnnotationShape;

public sealed class PolygonShapeData : IAnnotationShape
{
    public required IReadOnlyList<Point> Points { get; init; }
}

public sealed class CircleShapeData : IAnnotationShape
{
    public required Point Center { get; init; }

    public required double Radius { get; init; }
}

public sealed class EllipseShapeData : IAnnotationShape
{
    public required Point Center { get; init; }

    public required double RadiusX { get; init; }

    public required double RadiusY { get; init; }
}



public sealed class PanelMetricCalibration
{
    public double? PhysicalWidthMm { get; set; }

    public double? PhysicalHeightMm { get; set; }

    public bool IsConfigured => PhysicalWidthMm is > 0 && PhysicalHeightMm is > 0;
}

public sealed class RectificationResult
{
    public required IReadOnlyList<Point> OrderedSourceCorners { get; init; }

    public required IReadOnlyList<Point> OrderedDestinationCorners { get; init; }

    public required Size DestinationSizePixels { get; init; }

    public required double[] SourceToDestinationHomography { get; init; }

    public required BitmapSource WarpedImage { get; init; }

    public required IReadOnlyList<Point> TransformedHoleCenters { get; init; }
}
