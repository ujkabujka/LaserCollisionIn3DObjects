using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.SourceCompletion.ViewModels;

public sealed class SourceCompletionInputItem
{
    public required string Name { get; init; }
    public required ProjectedLightSourceItemViewModel Source { get; init; }
    public required string OriginText { get; init; }

    public string DisplayText => $"{Name} | {OriginText}";
}
