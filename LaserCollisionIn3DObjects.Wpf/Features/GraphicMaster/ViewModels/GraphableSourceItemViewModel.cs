using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;

public sealed class GraphableSourceItemViewModel : ObservableObject
{
    private bool _isSelected;

    public required GraphableSourceData SourceData { get; init; }

    public string DisplayName => SourceData.DisplayName;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
