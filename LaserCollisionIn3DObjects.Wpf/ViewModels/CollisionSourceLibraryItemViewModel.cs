using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

/// <summary>A reusable source definition. Scenes always receive a detached snapshot.</summary>
public sealed class CollisionSourceLibraryItemViewModel : ObservableObject
{
    public Guid SourceId { get; init; } = Guid.NewGuid();
    public CylindricalLightSourceItemViewModel? GeneratedSource { get; init; }
    public ProjectedLightSourceItemViewModel? TransferredSource { get; init; }
    public string Name => GeneratedSource?.Name ?? TransferredSource?.Name ?? "Source";
    public string SourceType => GeneratedSource?.SourceKind.ToString() ?? TransferredSource?.SourceTypeLabel ?? "Source";
    public int RayCount => GeneratedSource?.RayCount ?? TransferredSource?.EffectiveRayCount ?? 0;

    public CollisionSourceLibraryItemViewModel DeepClone(bool preserveIdentity = true) => new()
    {
        SourceId = preserveIdentity ? SourceId : Guid.NewGuid(),
        GeneratedSource = GeneratedSource?.DeepClone(),
        TransferredSource = TransferredSource?.DeepClone(),
    };
}
