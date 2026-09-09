using LaserCollisionIn3DObjects.Wpf.ViewModels;
using LaserCollisionIn3DObjects.Domain.SourceCompletion;

namespace LaserCollisionIn3DObjects.Wpf.Features.SourceCompletion.ViewModels;

public sealed class SourceCompletionInputItem
{
    public required ProjectedSourceIdentity Identity { get; init; }
    public string StableId => Identity.ToString();
    public required string Name { get; init; }
    public required ProjectedLightSourceItemViewModel Source { get; init; }
    public required string OriginText { get; init; }

    public string DisplayText => $"{Name} | {OriginText}";
}
