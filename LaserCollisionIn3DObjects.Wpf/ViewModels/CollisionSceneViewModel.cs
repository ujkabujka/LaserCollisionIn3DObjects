using System.Collections.ObjectModel;
using System.Windows.Media.Media3D;
using LaserCollisionIn3DObjects.Domain.Geometry;
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
    private List<Point3>? _holeCenters;


    public CollisionSceneViewModel(string name)
    {
        _name = name;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<PrismItemViewModel> Prisms { get; } = new();
    public ObservableCollection<RayItemViewModel> Rays { get; } = new();
    public ObservableCollection<CylindricalLightSourceItemViewModel> LightSources { get; } = new();
    public ObservableCollection<HitResultItemViewModel> HitResults { get; } = new();
    public List<Point3> HoleCenters = new();
   
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

}
