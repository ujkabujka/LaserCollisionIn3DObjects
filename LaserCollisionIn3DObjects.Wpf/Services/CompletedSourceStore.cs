using System.Collections.ObjectModel;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.SourceCompletion;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class CompletedSourceStore : ObservableObject
{
    private CompletedSourceItem? _selectedItem;
    public ObservableCollection<CompletedSourceItem> CompletedSources { get; } = new();
    public CompletedSourceItem? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
}

public sealed class CompletedSourceItem : ObservableObject
{
    private string _name = string.Empty;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public string Methodology { get; init; } = string.Empty;
    public string OriginalSourceName { get; init; } = string.Empty;
    public AxisymmetricSourceProfileDefinition ProfileDefinition { get; init; } = new();
    public PointSourceFrameState SourceFrame { get; init; } = new();
    public IReadOnlyList<ProjectionRay> OriginalRays { get; init; } = Array.Empty<ProjectionRay>();
    public IReadOnlyList<ProjectionRay> SyntheticRays { get; init; } = Array.Empty<ProjectionRay>();
    public IReadOnlyList<ProjectionRay> CompletedRays { get; init; } = Array.Empty<ProjectionRay>();
    public SourceCompletionSettings Settings { get; init; } = new(5, 10, true);
    public override string ToString() => Name;
}
