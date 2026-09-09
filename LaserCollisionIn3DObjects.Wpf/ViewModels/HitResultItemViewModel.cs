using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public sealed class HitResultItemViewModel : ObservableObject
{
    private string _rayLabel = string.Empty;
    private bool _hasHit;
    private float _distance;
    private string _prismName = "-";
    private string _sourceType = string.Empty;
    private string _sourceName = string.Empty;

    public string SourceType { get => _sourceType; set => SetProperty(ref _sourceType, value); }
    public string SourceName { get => _sourceName; set => SetProperty(ref _sourceName, value); }

    public string RayLabel
    {
        get => _rayLabel;
        set => SetProperty(ref _rayLabel, value);
    }

    public bool HasHit
    {
        get => _hasHit;
        set => SetProperty(ref _hasHit, value);
    }

    public float Distance
    {
        get => _distance;
        set => SetProperty(ref _distance, value);
    }

    public string PrismName
    {
        get => _prismName;
        set => SetProperty(ref _prismName, value);
    }
}
