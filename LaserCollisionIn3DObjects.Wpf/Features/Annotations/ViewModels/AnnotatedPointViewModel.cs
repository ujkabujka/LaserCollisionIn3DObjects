using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;

public sealed class AnnotatedPointViewModel
{
    public AnnotationPointCategory Category { get; init; }
    public required int Index { get; init; }

    public required AnnotationShapeType ShapeType { get; init; }

    public required string OriginalCenter { get; init; }

    public required string WarpedCenter { get; init; }

    public required string WarpedCenterMm { get; init; }

    public required Point WarpedCenterMmNumeric {get; init; }

    public required string PixelArea { get; init; }
}
