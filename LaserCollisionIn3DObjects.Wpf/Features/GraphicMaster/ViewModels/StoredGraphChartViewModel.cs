using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;

public sealed class StoredGraphChartViewModel : ObservableObject
{
    private string _displayName = string.Empty;

    public required string Id { get; init; }
    public required string GraphTypeId { get; init; }
    public required double BinSizeDeg { get; init; }
    public required IReadOnlyList<string> SelectedSourceIds { get; init; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }
}
