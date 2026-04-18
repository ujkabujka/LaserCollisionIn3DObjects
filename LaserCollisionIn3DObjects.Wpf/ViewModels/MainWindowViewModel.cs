using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Input;
using Microsoft.Win32;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;

namespace LaserCollisionIn3DObjects.Wpf.ViewModels;

public enum CollisionAlgorithmOption
{
    ClosestHitSequential,
    ClosestHitParallel,
}

public enum WorkspaceKind
{
    Collision,
    Annotation,
    Projection,
}

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly ObservableCollection<PrismItemViewModel> EmptyPrisms = new();
    private static readonly ObservableCollection<RayItemViewModel> EmptyRays = new();
    private static readonly ObservableCollection<CylindricalLightSourceItemViewModel> EmptyLightSources = new();
    private static readonly ObservableCollection<HitResultItemViewModel> EmptyHitResults = new();
    private static readonly ObservableCollection<Point3> EmptyHoles = new();
    private readonly SceneRenderSyncService _renderSyncService;
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly ProjectPersistenceCoordinator _projectPersistenceCoordinator = new();
    private string _newSceneName = "Scene 1";
    private string _newPrismName = "Prism 1";
    private float _newPrismSizeX = 0.002f;
    private float _newPrismSizeY = 1.2f;
    private float _newPrismSizeZ = 2.4f;
    private int _newPrismArrayCount = 8;
    private float _newPrismArrayRadius = 20f;
    private float _newPrismArrayLength = 20f;
    private PrismArrayPlacementMode _selectedPrismArrayPlacementMode = PrismArrayPlacementMode.Cylindrical;
    private float _newRayDirectionX = 1f;
    private string _newLightSourceName = "Light Source 1";
    private float _newLightSourceRadius = 5f;
    private float _newLightSourceHeight = 10f;
    private int _newLightSourceRayCount = 200;
    private CollisionAlgorithmOption _selectedCollisionAlgorithm = CollisionAlgorithmOption.ClosestHitSequential;
    private string _lastCollisionDurationMs = "N/A";
    private string _lastSequentialCollisionDurationMs = "N/A";
    private string _lastParallelCollisionDurationMs = "N/A";
    private string _statusMessage = "Add objects, then click Run Collision.";
    private bool _isNavigationCollapsed;
    private WorkspaceKind _selectedWorkspace = WorkspaceKind.Collision;

    public MainWindowViewModel(SceneRenderSyncService renderSyncService, ProjectionRenderSyncService projectionRenderSyncService)
    {
        _renderSyncService = renderSyncService ?? throw new ArgumentNullException(nameof(renderSyncService));
        ArgumentNullException.ThrowIfNull(projectionRenderSyncService);
        _sceneCollectionService = new SceneCollectionService();
        _sceneCollectionService.PropertyChanged += OnSceneCollectionPropertyChanged;

        AnnotationWorkspace = new AnnotationWorkspaceViewModel(_sceneCollectionService);
        ProjectionWorkspace = new ProjectionWorkspaceViewModel(_sceneCollectionService, projectionRenderSyncService);

        CreateSceneCommand = new RelayCommand(CreateScene);
        DeleteSelectedSceneCommand = new RelayCommand(DeleteSelectedScene, () => SelectedScene is not null);
        AddPrismCommand = new RelayCommand(AddPrism, () => SelectedScene is not null);
        AddPrismArrayCommand = new RelayCommand(AddPrismArray, () => SelectedScene is not null);
        AddRayCommand = new RelayCommand(AddRay, () => SelectedScene is not null);
        AddLightSourceCommand = new RelayCommand(AddLightSource, () => SelectedScene is not null);
        RemoveSelectedPrismCommand = new RelayCommand(RemoveSelectedPrism, () => SelectedPrism is not null);
        RemoveAllPrismsCommand = new RelayCommand(RemoveAllPrisms, () => Prisms.Count > 0);
        RemoveSelectedRayCommand = new RelayCommand(RemoveSelectedRay, () => SelectedRay is not null);
        RemoveAllRaysCommand = new RelayCommand(RemoveAllRays, () => Rays.Count > 0);
        RemoveSelectedLightSourceCommand = new RelayCommand(RemoveSelectedLightSource, () => SelectedLightSource is not null);
        RunCollisionCommand = new RelayCommand(RunCollision, () => SelectedScene is not null);
        RegenerateLightSourceRaysCommand = new RelayCommand(RegenerateLightSourceRays, () => SelectedScene is not null);
        ResetDemoSceneCommand = new RelayCommand(ResetDemoScene, () => SelectedScene is not null);
        SaveProjectCommand = new RelayCommand(SaveProject);
        LoadProjectCommand = new RelayCommand(LoadProject);
        SaveCollisionTabCommand = new RelayCommand(SaveCollisionTabState);
        LoadCollisionTabCommand = new RelayCommand(LoadCollisionTabState);
        SaveProjectionTabCommand = new RelayCommand(SaveProjectionTabState);
        LoadProjectionTabCommand = new RelayCommand(LoadProjectionTabState);
        SaveAnnotationTabCommand = new RelayCommand(SaveAnnotationTabState);
        LoadAnnotationTabCommand = new RelayCommand(LoadAnnotationTabState);
        ShowCollisionWorkspaceCommand = new RelayCommand(() => SelectedWorkspace = WorkspaceKind.Collision);
        ShowAnnotationWorkspaceCommand = new RelayCommand(() => SelectedWorkspace = WorkspaceKind.Annotation);
        ShowProjectionWorkspaceCommand = new RelayCommand(() => SelectedWorkspace = WorkspaceKind.Projection);

        CreateScene();
        RefreshViewport(false);
    }

    public string Title => "Laser Collision in 3D Objects";

    public AnnotationWorkspaceViewModel AnnotationWorkspace { get; }
    public ProjectionWorkspaceViewModel ProjectionWorkspace { get; }

    public ObservableCollection<CollisionSceneViewModel> Scenes => _sceneCollectionService.Scenes;

    public CollisionSceneViewModel? SelectedScene
    {
        get => _sceneCollectionService.SelectedScene;
        set
        {
            if (ReferenceEquals(_sceneCollectionService.SelectedScene, value))
            {
                return;
            }

            _sceneCollectionService.SelectedScene = value;
            if (value is not null)
            {
                StatusMessage = $"Selected scene '{value.Name}'.";
            }

            RefreshSceneBindingsAndViewport();
        }
    }

    public ObservableCollection<PrismItemViewModel> Prisms => SelectedScene?.Prisms ?? EmptyPrisms;
    public ObservableCollection<RayItemViewModel> Rays => SelectedScene?.Rays ?? EmptyRays;
    public ObservableCollection<CylindricalLightSourceItemViewModel> LightSources => SelectedScene?.LightSources ?? EmptyLightSources;
    public ObservableCollection<HitResultItemViewModel> HitResults => SelectedScene?.HitResults ?? EmptyHitResults;

    public PrismArrayPlacementMode[] PrismArrayPlacementModes { get; } = Enum.GetValues<PrismArrayPlacementMode>();
    public CollisionAlgorithmOption[] CollisionAlgorithms { get; } = Enum.GetValues<CollisionAlgorithmOption>();

    public ICommand CreateSceneCommand { get; }
    public ICommand DeleteSelectedSceneCommand { get; }
    public ICommand AddPrismCommand { get; }
    public ICommand AddPrismArrayCommand { get; }
    public ICommand AddRayCommand { get; }
    public ICommand AddLightSourceCommand { get; }
    public ICommand RemoveSelectedPrismCommand { get; }
    public ICommand RemoveAllPrismsCommand { get; }
    public ICommand RemoveSelectedRayCommand { get; }
    public ICommand RemoveAllRaysCommand { get; }
    public ICommand RemoveSelectedLightSourceCommand { get; }
    public ICommand RunCollisionCommand { get; }
    public ICommand RegenerateLightSourceRaysCommand { get; }
    public ICommand ResetDemoSceneCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand LoadProjectCommand { get; }
    public ICommand SaveCollisionTabCommand { get; }
    public ICommand LoadCollisionTabCommand { get; }
    public ICommand SaveProjectionTabCommand { get; }
    public ICommand LoadProjectionTabCommand { get; }
    public ICommand SaveAnnotationTabCommand { get; }
    public ICommand LoadAnnotationTabCommand { get; }
    public ICommand ShowCollisionWorkspaceCommand { get; }
    public ICommand ShowAnnotationWorkspaceCommand { get; }
    public ICommand ShowProjectionWorkspaceCommand { get; }

    public bool IsNavigationCollapsed
    {
        get => _isNavigationCollapsed;
        set
        {
            if (SetProperty(ref _isNavigationCollapsed, value))
            {
                RaisePropertyChanged(nameof(NavigationRailWidth));
            }
        }
    }

    public double NavigationRailWidth => IsNavigationCollapsed ? 64 : 220;

    public WorkspaceKind SelectedWorkspace
    {
        get => _selectedWorkspace;
        set => SetProperty(ref _selectedWorkspace, value);
    }

    public string NewSceneName { get => _newSceneName; set => SetProperty(ref _newSceneName, value); }

    public PrismItemViewModel? SelectedPrism
    {
        get => SelectedScene?.SelectedPrism;
        set
        {
            if (SelectedScene is null || Equals(SelectedScene.SelectedPrism, value))
            {
                return;
            }

            SelectedScene.SelectedPrism = value;
            RaiseCanExecuteChanges();
            RaisePropertyChanged();
        }
    }

    public RayItemViewModel? SelectedRay
    {
        get => SelectedScene?.SelectedRay;
        set
        {
            if (SelectedScene is null || Equals(SelectedScene.SelectedRay, value))
            {
                return;
            }

            SelectedScene.SelectedRay = value;
            RaiseCanExecuteChanges();
            RaisePropertyChanged();
        }
    }

    public CylindricalLightSourceItemViewModel? SelectedLightSource
    {
        get => SelectedScene?.SelectedLightSource;
        set
        {
            if (SelectedScene is null || Equals(SelectedScene.SelectedLightSource, value))
            {
                return;
            }

            SelectedScene.SelectedLightSource = value;
            RaiseCanExecuteChanges();
            RaisePropertyChanged();
        }
    }

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
    public CollisionAlgorithmOption SelectedCollisionAlgorithm
    {
        get => _selectedCollisionAlgorithm;
        set => SetProperty(ref _selectedCollisionAlgorithm, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string LastCollisionDurationMs { get => _lastCollisionDurationMs; private set => SetProperty(ref _lastCollisionDurationMs, value); }
    public string LastSequentialCollisionDurationMs { get => _lastSequentialCollisionDurationMs; private set => SetProperty(ref _lastSequentialCollisionDurationMs, value); }
    public string LastParallelCollisionDurationMs { get => _lastParallelCollisionDurationMs; private set => SetProperty(ref _lastParallelCollisionDurationMs, value); }

    private void CreateScene()
    {
        var scene = _sceneCollectionService.CreateScene(NewSceneName);
        SelectedScene = scene;
        NewSceneName = $"Scene {Scenes.Count + 1}";
        StatusMessage = $"Created scene '{scene.Name}'.";
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void DeleteSelectedScene()
    {
        if (SelectedScene is null)
        {
            StatusMessage = "Select a scene to delete.";
            return;
        }

        var deletedName = SelectedScene.Name;
        _sceneCollectionService.RemoveScene(SelectedScene);
        StatusMessage = Scenes.Count == 0
            ? $"Deleted '{deletedName}'. Workspace is empty."
            : $"Deleted '{deletedName}'.";

        RefreshSceneBindingsAndViewport();
    }

    private void AddPrism()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        if (!ValidatePrismInputs(NewPrismSizeX, NewPrismSizeY, NewPrismSizeZ, out var error))
        {
            StatusMessage = error;
            return;
        }

        scene.Prisms.Add(CreatePrismViewModel(
            string.IsNullOrWhiteSpace(NewPrismName) ? $"Prism {scene.Prisms.Count + 1}" : NewPrismName,
            new Vector3(NewPrismPosX, NewPrismPosY, NewPrismPosZ),
            Quaternion.Identity));

        scene.SelectedPrism = scene.Prisms.Last();
        NewPrismName = $"Prism {scene.Prisms.Count + 1}";
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void AddPrismArray()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

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
            scene.Prisms.Add(prism);
        }

        scene.SelectedPrism = created.LastOrDefault();
        NewPrismName = $"Prism {scene.Prisms.Count + 1}";
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        StatusMessage = $"Added {created.Count} prisms in a {SelectedPrismArrayPlacementMode} array around the world origin with global-axis-aligned default frames.";
    }

    private void AddRay()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        if (!ValidateDirection(NewRayDirectionX, NewRayDirectionY, NewRayDirectionZ, out var error))
        {
            StatusMessage = error;
            return;
        }

        scene.Rays.Add(new RayItemViewModel
        {
            OriginX = NewRayOriginX,
            OriginY = NewRayOriginY,
            OriginZ = NewRayOriginZ,
            DirectionX = NewRayDirectionX,
            DirectionY = NewRayDirectionY,
            DirectionZ = NewRayDirectionZ,
        });

        scene.SelectedRay = scene.Rays.Last();
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void AddLightSource()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        if (!ValidateLightSourceInputs(NewLightSourceRadius, NewLightSourceHeight, NewLightSourceRayCount, out var error))
        {
            StatusMessage = error;
            return;
        }

        scene.LightSources.Add(new CylindricalLightSourceItemViewModel
        {
            Name = string.IsNullOrWhiteSpace(NewLightSourceName) ? $"Light Source {scene.LightSources.Count + 1}" : NewLightSourceName,
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

        scene.SelectedLightSource = scene.LightSources.Last();
        NewLightSourceName = $"Light Source {scene.LightSources.Count + 1}";
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void RemoveSelectedPrism()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene?.SelectedPrism is null)
        {
            StatusMessage = "Select a prism to remove.";
            return;
        }

        scene.Prisms.Remove(scene.SelectedPrism);
        scene.SelectedPrism = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void RemoveAllPrisms()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        if (scene.Prisms.Count == 0)
        {
            StatusMessage = "There are no prisms to delete.";
            return;
        }

        var deleted = scene.Prisms.Count;
        scene.Prisms.Clear();
        scene.SelectedPrism = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        StatusMessage = $"Deleted {deleted} prisms.";
    }

    private void RemoveAllRays()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        if (scene.Rays.Count == 0)
        {
            StatusMessage = "There are no rays to delete.";
            return;
        }

        var deleted = scene.Rays.Count;
        scene.Rays.Clear();
        scene.SelectedRay = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        StatusMessage = $"Deleted {deleted} rays.";
    }

    private void RemoveSelectedRay()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene?.SelectedRay is null)
        {
            StatusMessage = "Select a ray to remove.";
            return;
        }

        scene.Rays.Remove(scene.SelectedRay);
        scene.SelectedRay = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void RemoveSelectedLightSource()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene?.SelectedLightSource is null)
        {
            StatusMessage = "Select a light source to remove.";
            return;
        }

        scene.LightSources.Remove(scene.SelectedLightSource);
        scene.SelectedLightSource = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void RunCollision()
    {
        if (SelectedScene is null)
        {
            StatusMessage = "Create or select a scene first.";
            return;
        }

        if (!ValidateAllSceneItems(out var error))
        {
            StatusMessage = error;
            return;
        }

        RefreshViewport(true);
    }

    private void RegenerateLightSourceRays()
    {
        if (SelectedScene is null)
        {
            StatusMessage = "Create or select a scene first.";
            return;
        }

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
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        scene.Prisms.Clear();
        scene.Rays.Clear();
        scene.LightSources.Clear();

        scene.Prisms.Add(CreatePrismViewModel("Prism 1", new Vector3(0f, 0f, 0f), Quaternion.Identity, sizeX: 10f, sizeY: 10f, sizeZ: 10f));
        scene.Prisms.Add(CreatePrismViewModel("Prism 2", new Vector3(16f, 0f, 0f), Quaternion.Identity, sizeX: 8f, sizeY: 8f, sizeZ: 8f));

        scene.Rays.Add(new RayItemViewModel { OriginX = -30, OriginY = 0, OriginZ = 0, DirectionX = 1, DirectionY = 0, DirectionZ = 0 });

        scene.LightSources.Add(new CylindricalLightSourceItemViewModel
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

        scene.SelectedPrism = null;
        scene.SelectedRay = null;
        scene.SelectedLightSource = null;

        RefreshViewport(true);
        StatusMessage = "Demo scene reset with manual and generated rays.";
        RaiseCanExecuteChanges();
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
            var scene = SelectedScene;
            var prisms = scene?.Prisms ?? EmptyPrisms;
            var lightSources = scene?.LightSources ?? EmptyLightSources;
            var rays = scene?.Rays ?? EmptyRays;
            var holes = scene?.HolePoints ?? EmptyHoles;
            var projectionResult = scene?.ProjectionState.SelectedResult;

            var sceneSyncResult = _renderSyncService.SyncScene(prisms, lightSources, rays, holes, projectionResult, runCollision, SelectedCollisionAlgorithm);
            var rows = sceneSyncResult.HitRows;

            if (scene is not null)
            {
                scene.HitResults.Clear();
                foreach (var row in rows)
                {
                    scene.HitResults.Add(row);
                }
            }

            RaisePropertyChanged(nameof(HitResults));

            if (runCollision)
            {
                var elapsedMs = sceneSyncResult.CollisionDuration.TotalMilliseconds;
                LastCollisionDurationMs = $"{elapsedMs:F3}";

                if (sceneSyncResult.CollisionAlgorithm == CollisionAlgorithmOption.ClosestHitSequential)
                {
                    LastSequentialCollisionDurationMs = LastCollisionDurationMs;
                }
                else if (sceneSyncResult.CollisionAlgorithm == CollisionAlgorithmOption.ClosestHitParallel)
                {
                    LastParallelCollisionDurationMs = LastCollisionDurationMs;
                }

                StatusMessage = $"Collision run complete ({SelectedCollisionAlgorithm}) in {elapsedMs:F3} ms. Hits: {rows.Count(r => r.HasHit)}/{rows.Count}.";
            }
            else
            {
                StatusMessage = scene is null
                    ? "No scene selected. Create a scene to begin."
                    : $"Scene refreshed. Manual rays: {rays.Count}, generated rays: {lightSources.Sum(s => Math.Max(0, s.RayCount))}.";
            }
        }
        catch (ArgumentException ex)
        {
            StatusMessage = $"Please check values: {ex.Message}";
            SelectedScene?.HitResults.Clear();
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

    private CollisionSceneViewModel? GetSelectedSceneOrSetStatus()
    {
        if (SelectedScene is not null)
        {
            return SelectedScene;
        }

        StatusMessage = "Create or select a scene first.";
        return null;
    }

    private void OnSceneCollectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneCollectionService.SelectedScene))
        {
            RefreshSceneBindingsAndViewport();
        }
    }

    private void SaveProject()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Laser Collision Project (*.lc3d.json)|*.lc3d.json|JSON (*.json)|*.json",
            FileName = "project.lc3d.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.SaveProject(dialog.FileName, _sceneCollectionService, SelectedScene, AnnotationWorkspace, ProjectionWorkspace);
        StatusMessage = $"Project saved to '{dialog.FileName}'.";
    }

    private void LoadProject()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Laser Collision Project (*.lc3d.json)|*.lc3d.json|JSON (*.json)|*.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.LoadProject(dialog.FileName, _sceneCollectionService, AnnotationWorkspace, ProjectionWorkspace);
        RefreshSceneBindingsAndViewport();
        StatusMessage = $"Project loaded from '{dialog.FileName}'.";
    }

    private void SaveCollisionTabState()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Collision Tab State (*.collision.json)|*.collision.json|JSON (*.json)|*.json",
            FileName = "collision-tab.collision.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.SaveCollisionTab(dialog.FileName, _sceneCollectionService, SelectedScene);
        StatusMessage = $"Collision tab state saved to '{dialog.FileName}'.";
    }

    private void LoadCollisionTabState()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Collision Tab State (*.collision.json)|*.collision.json|JSON (*.json)|*.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.LoadCollisionTab(dialog.FileName, _sceneCollectionService);
        RefreshSceneBindingsAndViewport();
        StatusMessage = $"Collision tab state loaded from '{dialog.FileName}'.";
    }

    private void SaveProjectionTabState()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Projection Tab State (*.projection.json)|*.projection.json|JSON (*.json)|*.json",
            FileName = "projection-tab.projection.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.SaveProjectionTab(dialog.FileName, _sceneCollectionService, ProjectionWorkspace);
        StatusMessage = $"Projection tab state saved to '{dialog.FileName}'.";
    }

    private void LoadProjectionTabState()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Projection Tab State (*.projection.json)|*.projection.json|JSON (*.json)|*.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.LoadProjectionTab(dialog.FileName, _sceneCollectionService, ProjectionWorkspace);
        RefreshSceneBindingsAndViewport();
        StatusMessage = $"Projection tab state loaded from '{dialog.FileName}'.";
    }

    private void SaveAnnotationTabState()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Annotation Tab State (*.annotation.json)|*.annotation.json|JSON (*.json)|*.json",
            FileName = "annotation-tab.annotation.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.SaveAnnotationTab(dialog.FileName, AnnotationWorkspace);
        StatusMessage = $"Annotation tab state saved to '{dialog.FileName}'.";
    }

    private void LoadAnnotationTabState()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Annotation Tab State (*.annotation.json)|*.annotation.json|JSON (*.json)|*.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _projectPersistenceCoordinator.LoadAnnotationTab(dialog.FileName, AnnotationWorkspace);
        StatusMessage = $"Annotation tab state loaded from '{dialog.FileName}'.";
    }

    private void RefreshSceneBindingsAndViewport()
    {
        RaisePropertyChanged(nameof(SelectedScene));
        RaisePropertyChanged(nameof(Prisms));
        RaisePropertyChanged(nameof(Rays));
        RaisePropertyChanged(nameof(LightSources));
        RaisePropertyChanged(nameof(HitResults));
        RaisePropertyChanged(nameof(SelectedPrism));
        RaisePropertyChanged(nameof(SelectedRay));
        RaisePropertyChanged(nameof(SelectedLightSource));
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void RaiseCanExecuteChanges()
    {
        if (CreateSceneCommand is RelayCommand createSceneCommand)
        {
            createSceneCommand.RaiseCanExecuteChanged();
        }

        if (DeleteSelectedSceneCommand is RelayCommand deleteSceneCommand)
        {
            deleteSceneCommand.RaiseCanExecuteChanged();
        }

        if (AddPrismCommand is RelayCommand addPrismCommand)
        {
            addPrismCommand.RaiseCanExecuteChanged();
        }

        if (AddPrismArrayCommand is RelayCommand addPrismArrayCommand)
        {
            addPrismArrayCommand.RaiseCanExecuteChanged();
        }

        if (AddRayCommand is RelayCommand addRayCommand)
        {
            addRayCommand.RaiseCanExecuteChanged();
        }

        if (AddLightSourceCommand is RelayCommand addLightSourceCommand)
        {
            addLightSourceCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedPrismCommand is RelayCommand prismCommand)
        {
            prismCommand.RaiseCanExecuteChanged();
        }

        if (RemoveAllPrismsCommand is RelayCommand removeAllPrismsCommand)
        {
            removeAllPrismsCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedRayCommand is RelayCommand rayCommand)
        {
            rayCommand.RaiseCanExecuteChanged();
        }

        if (RemoveAllRaysCommand is RelayCommand removeAllRaysCommand)
        {
            removeAllRaysCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedLightSourceCommand is RelayCommand lightCommand)
        {
            lightCommand.RaiseCanExecuteChanged();
        }

        if (RunCollisionCommand is RelayCommand runCollisionCommand)
        {
            runCollisionCommand.RaiseCanExecuteChanged();
        }

        if (RegenerateLightSourceRaysCommand is RelayCommand regenerateCommand)
        {
            regenerateCommand.RaiseCanExecuteChanged();
        }

        if (ResetDemoSceneCommand is RelayCommand resetDemoCommand)
        {
            resetDemoCommand.RaiseCanExecuteChanged();
        }
    }
}
