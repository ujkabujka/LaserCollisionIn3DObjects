using System.Collections.ObjectModel;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Scene;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

/// <summary>
/// Represents one editable collision scene in the workspace.
/// </summary>
public sealed class CollisionSceneViewModel : ObservableObject
{
    private string _name;
    private PrismItemViewModel? _selectedPrism;
    private RayItemViewModel? _selectedRay;
    private CylindricalLightSourceItemViewModel? _selectedLightSource;
    private ProjectedLightSourceItemViewModel? _selectedProjectedLightSource;
    private bool _isProjectionOnly;
    public CollisionSceneViewModel(string name)
    {
        _name = name;
        Prisms.CollectionChanged += (_, _) => InvalidateCollisionResults();
        Rays.CollectionChanged += (_, _) => InvalidateCollisionResults();
        LightSources.CollectionChanged += (_, _) => InvalidateCollisionResults();
        ProjectedLightSources.CollectionChanged += (_, _) => InvalidateCollisionResults();
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<PrismItemViewModel> Prisms { get; } = new();
    public ObservableCollection<RayItemViewModel> Rays { get; } = new();
    public ObservableCollection<CylindricalLightSourceItemViewModel> LightSources { get; } = new();
    public ObservableCollection<ProjectedLightSourceItemViewModel> ProjectedLightSources { get; } = new();
    public ObservableCollection<HitResultItemViewModel> HitResults { get; } = new();
    public IReadOnlyList<CollisionHitPointRecord> HitPointRecords { get; private set; } = Array.Empty<CollisionHitPointRecord>();
    public bool HasValidCollisionRun { get; private set; }

    public void PublishCollisionResults(IEnumerable<HitResultItemViewModel> rows, IReadOnlyList<CollisionHitPointRecord> records)
    {
        HitResults.Clear();
        foreach (var row in rows) HitResults.Add(row);
        HitPointRecords = records;
        HasValidCollisionRun = true;
        RaisePropertyChanged(nameof(HitPointRecords));
        RaisePropertyChanged(nameof(HasValidCollisionRun));
    }

    public void InvalidateCollisionResults()
    {
        HitResults.Clear();
        HitPointRecords = Array.Empty<CollisionHitPointRecord>();
        HasValidCollisionRun = false;
        RaisePropertyChanged(nameof(HitPointRecords));
        RaisePropertyChanged(nameof(HasValidCollisionRun));
    }
    public ObservableCollection<Point3> HolePoints { get; } = new();
    public ObservableCollection<Point3> NaturalPoints { get; } = new();
    public ObservableCollection<Point3> MeasuredCornerPoints { get; } = new();
    public SceneProjectionState ProjectionState { get; } = new();
    public bool HasHolePoints => HolePoints.Count > 0;
    public bool IsProjectionOnly
    {
        get => _isProjectionOnly;
        set => SetProperty(ref _isProjectionOnly, value);
    }

    public PrismItemViewModel? SelectedPrism
    {
        get => _selectedPrism;
        set => SetProperty(ref _selectedPrism, value);
    }

    public RayItemViewModel? SelectedRay
    {
        get => _selectedRay;
        set => SetProperty(ref _selectedRay, value);
    }

    public CylindricalLightSourceItemViewModel? SelectedLightSource
    {
        get => _selectedLightSource;
        set => SetProperty(ref _selectedLightSource, value);
    }

    public ProjectedLightSourceItemViewModel? SelectedProjectedLightSource
    {
        get => _selectedProjectedLightSource;
        set => SetProperty(ref _selectedProjectedLightSource, value);
    }

}
