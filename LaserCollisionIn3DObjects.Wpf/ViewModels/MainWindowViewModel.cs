using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Input;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly SceneRenderSyncService _renderSyncService;
    private PrismItemViewModel? _selectedPrism;
    private RayItemViewModel? _selectedRay;
    private CylindricalLightSourceItemViewModel? _selectedLightSource;
    private string _newPrismName = "Prism 1";
    private float _newPrismSizeX = 10f;
    private float _newPrismSizeY = 10f;
    private float _newPrismSizeZ = 10f;
    private int _newPrismArrayCount = 8;
    private float _newPrismArrayRadius = 20f;
    private float _newPrismArrayLength = 20f;
    private PrismArrayPlacementMode _selectedPrismArrayPlacementMode = PrismArrayPlacementMode.Cylindrical;
    private float _newRayDirectionX = 1f;
    private string _newLightSourceName = "Light Source 1";
    private float _newLightSourceRadius = 5f;
    private float _newLightSourceHeight = 10f;
    private int _newLightSourceRayCount = 200;
    private string _statusMessage = "Add objects, then click Run Collision.";

    public MainWindowViewModel(SceneRenderSyncService renderSyncService)
    {
        _renderSyncService = renderSyncService ?? throw new ArgumentNullException(nameof(renderSyncService));

        AddPrismCommand = new RelayCommand(AddPrism);
        AddPrismArrayCommand = new RelayCommand(AddPrismArray);
        AddRayCommand = new RelayCommand(AddRay);
        AddLightSourceCommand = new RelayCommand(AddLightSource);
        RemoveSelectedPrismCommand = new RelayCommand(RemoveSelectedPrism, () => SelectedPrism is not null);
        RemoveSelectedRayCommand = new RelayCommand(RemoveSelectedRay, () => SelectedRay is not null);
        RemoveSelectedLightSourceCommand = new RelayCommand(RemoveSelectedLightSource, () => SelectedLightSource is not null);
        RunCollisionCommand = new RelayCommand(RunCollision);
        RegenerateLightSourceRaysCommand = new RelayCommand(RegenerateLightSourceRays);
        ResetDemoSceneCommand = new RelayCommand(ResetDemoScene);

        RefreshViewport(false);
    }

    public string Title => "Laser Collision in 3D Objects";

    public ObservableCollection<PrismItemViewModel> Prisms { get; } = new();
    public ObservableCollection<RayItemViewModel> Rays { get; } = new();
    public ObservableCollection<CylindricalLightSourceItemViewModel> LightSources { get; } = new();
    public ObservableCollection<HitResultItemViewModel> HitResults { get; } = new();

    public PrismArrayPlacementMode[] PrismArrayPlacementModes { get; } = Enum.GetValues<PrismArrayPlacementMode>();

    public ICommand AddPrismCommand { get; }
    public ICommand AddPrismArrayCommand { get; }
    public ICommand AddRayCommand { get; }
    public ICommand AddLightSourceCommand { get; }
    public ICommand RemoveSelectedPrismCommand { get; }
    public ICommand RemoveSelectedRayCommand { get; }
    public ICommand RemoveSelectedLightSourceCommand { get; }
    public ICommand RunCollisionCommand { get; }
    public ICommand RegenerateLightSourceRaysCommand { get; }
    public ICommand ResetDemoSceneCommand { get; }

    public PrismItemViewModel? SelectedPrism { get => _selectedPrism; set { if (SetProperty(ref _selectedPrism, value)) RaiseCanExecuteChanges(); } }
    public RayItemViewModel? SelectedRay { get => _selectedRay; set { if (SetProperty(ref _selectedRay, value)) RaiseCanExecuteChanges(); } }
    public CylindricalLightSourceItemViewModel? SelectedLightSource { get => _selectedLightSource; set { if (SetProperty(ref _selectedLightSource, value)) RaiseCanExecuteChanges(); } }

    public string NewPrismName { get => _newPrismName; set => SetProperty(ref _newPrismName, value); }
    public float NewPrismPosX { get; set; }
    public float NewPrismPosY { get; set; }
    public float NewPrismPosZ { get; set; }
    public float NewPrismRotX { get; set; }
    public float NewPrismRotY { get; set; }
    public float NewPrismRotZ { get; set; }
    public float NewPrismSizeX { get => _newPrismSizeX; set => SetProperty(ref _newPrismSizeX, value); }
    public float NewPrismSizeY { get => _newPrismSizeY; set => SetProperty(ref _newPrismSizeY, value); }
    public float NewPrismSizeZ { get => _newPrismSizeZ; set => SetProperty(ref _newPrismSizeZ, value); }
    public int NewPrismArrayCount { get => _newPrismArrayCount; set => SetProperty(ref _newPrismArrayCount, value); }
    public float NewPrismArrayRadius { get => _newPrismArrayRadius; set => SetProperty(ref _newPrismArrayRadius, value); }
    public float NewPrismArrayLength { get => _newPrismArrayLength; set => SetProperty(ref _newPrismArrayLength, value); }
    public PrismArrayPlacementMode SelectedPrismArrayPlacementMode
    {
        get => _selectedPrismArrayPlacementMode;
        set => SetProperty(ref _selectedPrismArrayPlacementMode, value);
    }

    public float NewRayOriginX { get; set; }
    public float NewRayOriginY { get; set; }
    public float NewRayOriginZ { get; set; }
    public float NewRayDirectionX { get => _newRayDirectionX; set => SetProperty(ref _newRayDirectionX, value); }
    public float NewRayDirectionY { get; set; }
    public float NewRayDirectionZ { get; set; }

    public string NewLightSourceName { get => _newLightSourceName; set => SetProperty(ref _newLightSourceName, value); }
    public float NewLightSourcePosX { get; set; }
    public float NewLightSourcePosY { get; set; }
    public float NewLightSourcePosZ { get; set; }
    public float NewLightSourceRotX { get; set; }
    public float NewLightSourceRotY { get; set; }
    public float NewLightSourceRotZ { get; set; }
    public float NewLightSourceRadius { get => _newLightSourceRadius; set => SetProperty(ref _newLightSourceRadius, value); }
    public float NewLightSourceHeight { get => _newLightSourceHeight; set => SetProperty(ref _newLightSourceHeight, value); }
    public int NewLightSourceRayCount { get => _newLightSourceRayCount; set => SetProperty(ref _newLightSourceRayCount, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private void AddPrism()
    {
        if (!ValidatePrismInputs(NewPrismSizeX, NewPrismSizeY, NewPrismSizeZ, out var error))
        {
            StatusMessage = error;
            return;
        }

        Prisms.Add(CreatePrismViewModel(
            string.IsNullOrWhiteSpace(NewPrismName) ? $"Prism {Prisms.Count + 1}" : NewPrismName,
            new Vector3(NewPrismPosX, NewPrismPosY, NewPrismPosZ),
            Quaternion.Identity));

        SelectedPrism = Prisms.Last();
        NewPrismName = $"Prism {Prisms.Count + 1}";
        RefreshViewport(false);
    }

    private void AddPrismArray()
    {
        if (!ValidatePrismInputs(NewPrismSizeX, NewPrismSizeY, NewPrismSizeZ, out var error))
        {
            StatusMessage = error;
            return;
        }

        if (!ValidatePrismArrayInputs(SelectedPrismArrayPlacementMode, NewPrismArrayCount, NewPrismArrayRadius, NewPrismArrayLength, out error))
        {
            StatusMessage = error;
            return;
        }

        var placements = SelectedPrismArrayPlacementMode switch
        {
            PrismArrayPlacementMode.Cylindrical => PrismPlacementGenerator.CreateCylindricalPlacements(NewPrismArrayRadius, NewPrismArrayCount, NewPrismPosY),
            PrismArrayPlacementMode.Cartesian => PrismPlacementGenerator.CreateCartesianPlacements(NewPrismArrayLength, NewPrismArrayCount, NewPrismPosY),
            _ => throw new InvalidOperationException("Unsupported prism array placement mode."),
        };

        var baseName = string.IsNullOrWhiteSpace(NewPrismName) ? "Prism" : NewPrismName;
        var created = new List<PrismItemViewModel>(placements.Count);

        for (var i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            created.Add(CreatePrismViewModel($"{baseName} {i + 1}", placement.Position, placement.Orientation));
        }

        foreach (var prism in created)
        {
            Prisms.Add(prism);
        }

        SelectedPrism = created.LastOrDefault();
        NewPrismName = $"Prism {Prisms.Count + 1}";
        RefreshViewport(false);
        StatusMessage = $"Added {created.Count} prisms in a {SelectedPrismArrayPlacementMode} array around the world origin.";
    }

    private void AddRay()
    {
        if (!ValidateDirection(NewRayDirectionX, NewRayDirectionY, NewRayDirectionZ, out var error))
        {
            StatusMessage = error;
            return;
        }

        Rays.Add(new RayItemViewModel
        {
            OriginX = NewRayOriginX,
            OriginY = NewRayOriginY,
            OriginZ = NewRayOriginZ,
            DirectionX = NewRayDirectionX,
            DirectionY = NewRayDirectionY,
            DirectionZ = NewRayDirectionZ,
        });

        SelectedRay = Rays.Last();
        RefreshViewport(false);
    }

    private void AddLightSource()
    {
        if (!ValidateLightSourceInputs(NewLightSourceRadius, NewLightSourceHeight, NewLightSourceRayCount, out var error))
        {
            StatusMessage = error;
            return;
        }

        LightSources.Add(new CylindricalLightSourceItemViewModel
        {
            Name = string.IsNullOrWhiteSpace(NewLightSourceName) ? $"Light Source {LightSources.Count + 1}" : NewLightSourceName,
            PositionX = NewLightSourcePosX,
            PositionY = NewLightSourcePosY,
            PositionZ = NewLightSourcePosZ,
            RotationX = NewLightSourceRotX,
            RotationY = NewLightSourceRotY,
            RotationZ = NewLightSourceRotZ,
            Radius = NewLightSourceRadius,
            Height = NewLightSourceHeight,
            RayCount = NewLightSourceRayCount,
            BaseOrientation = Quaternion.Identity,
        });

        SelectedLightSource = LightSources.Last();
        NewLightSourceName = $"Light Source {LightSources.Count + 1}";
        RefreshViewport(false);
    }

    private void RemoveSelectedPrism()
    {
        if (SelectedPrism is null)
        {
            StatusMessage = "Select a prism to remove.";
            return;
        }

        Prisms.Remove(SelectedPrism);
        SelectedPrism = null;
        RefreshViewport(false);
    }

    private void RemoveSelectedRay()
    {
        if (SelectedRay is null)
        {
            StatusMessage = "Select a ray to remove.";
            return;
        }

        Rays.Remove(SelectedRay);
        SelectedRay = null;
        RefreshViewport(false);
    }

    private void RemoveSelectedLightSource()
    {
        if (SelectedLightSource is null)
        {
            StatusMessage = "Select a light source to remove.";
            return;
        }

        LightSources.Remove(SelectedLightSource);
        SelectedLightSource = null;
        RefreshViewport(false);
    }

    private void RunCollision()
    {
        if (!ValidateAllSceneItems(out var error))
        {
            StatusMessage = error;
            return;
        }

        RefreshViewport(true);
    }

    private void RegenerateLightSourceRays()
    {
        if (!ValidateAllSceneItems(out var error))
        {
            StatusMessage = error;
            return;
        }

        RefreshViewport(false);
        StatusMessage = "Generated rays refreshed from cylindrical light sources.";
    }

    private void ResetDemoScene()
    {
        Prisms.Clear();
        Rays.Clear();
        LightSources.Clear();

        Prisms.Add(CreatePrismViewModel("Prism 1", new Vector3(0f, 0f, 0f), Quaternion.Identity, sizeX: 10f, sizeY: 10f, sizeZ: 10f));
        Prisms.Add(CreatePrismViewModel("Prism 2", new Vector3(16f, 0f, 0f), Quaternion.Identity, sizeX: 8f, sizeY: 8f, sizeZ: 8f));

        Rays.Add(new RayItemViewModel { OriginX = -30, OriginY = 0, OriginZ = 0, DirectionX = 1, DirectionY = 0, DirectionZ = 0 });

        LightSources.Add(new CylindricalLightSourceItemViewModel
        {
            Name = "Light Source 1",
            PositionX = -10,
            PositionY = 0,
            PositionZ = 0,
            Radius = 4,
            Height = 10,
            RayCount = 120,
            BaseOrientation = Quaternion.Identity,
        });

        RefreshViewport(true);
        StatusMessage = "Demo scene reset with manual and generated rays.";
    }

    private PrismItemViewModel CreatePrismViewModel(
        string name,
        Vector3 position,
        Quaternion baseOrientation,
        float? sizeX = null,
        float? sizeY = null,
        float? sizeZ = null)
    {
        return new PrismItemViewModel
        {
            Name = name,
            PositionX = position.X,
            PositionY = position.Y,
            PositionZ = position.Z,
            RotationX = NewPrismRotX,
            RotationY = NewPrismRotY,
            RotationZ = NewPrismRotZ,
            SizeX = sizeX ?? NewPrismSizeX,
            SizeY = sizeY ?? NewPrismSizeY,
            SizeZ = sizeZ ?? NewPrismSizeZ,
            BaseOrientation = baseOrientation,
        };
    }

    private void RefreshViewport(bool runCollision)
    {
        try
        {
            var rows = _renderSyncService.SyncScene(Prisms, LightSources, Rays, runCollision);
            HitResults.Clear();
            foreach (var row in rows)
            {
                HitResults.Add(row);
            }

            if (runCollision)
            {
                StatusMessage = $"Collision run complete. Hits: {rows.Count(r => r.HasHit)}/{rows.Count}.";
            }
            else
            {
                StatusMessage = $"Scene refreshed. Manual rays: {Rays.Count}, generated rays: {LightSources.Sum(s => Math.Max(0, s.RayCount))}.";
            }
        }
        catch (ArgumentException ex)
        {
            StatusMessage = $"Please check values: {ex.Message}";
            HitResults.Clear();
        }
    }

    private bool ValidateAllSceneItems(out string error)
    {
        foreach (var prism in Prisms)
        {
            if (!ValidatePrismInputs(prism.SizeX, prism.SizeY, prism.SizeZ, out error))
            {
                error = $"Prism '{prism.Name}' invalid. {error}";
                return false;
            }
        }

        for (var i = 0; i < Rays.Count; i++)
        {
            if (!ValidateDirection(Rays[i].DirectionX, Rays[i].DirectionY, Rays[i].DirectionZ, out error))
            {
                error = $"Manual ray {i + 1} invalid. {error}";
                return false;
            }
        }

        for (var i = 0; i < LightSources.Count; i++)
        {
            var source = LightSources[i];
            if (!ValidateLightSourceInputs(source.Radius, source.Height, source.RayCount, out error))
            {
                error = $"Light source {i + 1} invalid. {error}";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidatePrismInputs(float sx, float sy, float sz, out string error)
    {
        if (sx <= 0 || sy <= 0 || sz <= 0)
        {
            error = "Prism sizes must be positive.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidatePrismArrayInputs(
        PrismArrayPlacementMode mode,
        int count,
        float radius,
        float length,
        out string error)
    {
        if (count <= 0)
        {
            error = "Prism array count must be greater than zero.";
            return false;
        }

        if (mode == PrismArrayPlacementMode.Cylindrical && radius <= 0f)
        {
            error = "Cylindrical prism arrays require a positive radius.";
            return false;
        }

        if (mode == PrismArrayPlacementMode.Cartesian && length <= 0f)
        {
            error = "Cartesian prism arrays require a positive length.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateLightSourceInputs(float radius, float height, int rayCount, out string error)
    {
        if (radius <= 0 || height <= 0)
        {
            error = "Light source radius and height must be positive.";
            return false;
        }

        if (rayCount <= 0)
        {
            error = "Light source RayCount must be greater than zero.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool ValidateDirection(float x, float y, float z, out string error)
    {
        if (MathF.Abs(x) < float.Epsilon && MathF.Abs(y) < float.Epsilon && MathF.Abs(z) < float.Epsilon)
        {
            error = "Direction cannot be zero.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RaiseCanExecuteChanges()
    {
        if (RemoveSelectedPrismCommand is RelayCommand prismCommand)
        {
            prismCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedRayCommand is RelayCommand rayCommand)
        {
            rayCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedLightSourceCommand is RelayCommand lightCommand)
        {
            lightCommand.RaiseCanExecuteChanged();
        }
    }
}
