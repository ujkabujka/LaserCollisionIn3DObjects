using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using LaserCollisionIn3DObjects.Wpf;
using LaserCollisionIn3DObjects.Domain.Export;
using Microsoft.Win32;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Features.SourceCompletion.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Domain.Projection;

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
    GraphicMaster,
    SourceCompletion,
}

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly ObservableCollection<PrismItemViewModel> EmptyPrisms = new();
    private static readonly ObservableCollection<RayItemViewModel> EmptyRays = new();
    private static readonly ObservableCollection<CylindricalLightSourceItemViewModel> EmptyLightSources = new();
    private static readonly ObservableCollection<ProjectedLightSourceItemViewModel> EmptyProjectedLightSources = new();
    private static readonly ObservableCollection<HitResultItemViewModel> EmptyHitResults = new();
    private static readonly ObservableCollection<Point3> EmptyHoles = new();
    private readonly SceneRenderSyncService _renderSyncService;
    private readonly CollisionHitPointCsvExportService _collisionHitPointCsvExportService = new();
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly CompletedSourceStore _completedSourceStore = new();
    private readonly ProjectPersistenceCoordinator _projectPersistenceCoordinator = new();
    private IReadOnlyList<CollisionHitPointRecord> _lastCollisionHitPointRecords = Array.Empty<CollisionHitPointRecord>();
    private string _newSceneName = "Scene 1";
    private string _newPrismName = "Prism 1";
    private float _newPrismSizeX = 0.002f;
    private float _newPrismSizeY = 1.2f;
    private float _newPrismSizeZ = 2.4f;
    private int _newPrismArrayCount = 8;
    private float _newPrismArrayRadius = 10f;
    private float _newPrismArrayLength = 20f;
    private PrismArrayPlacementMode _selectedPrismArrayPlacementMode = PrismArrayPlacementMode.Cylindrical;
    private float _newRayDirectionX = 1f;
    private string _newLightSourceName = "Light Source 1";
    private AxisymmetricSourceKind _newLightSourceKind = AxisymmetricSourceKind.Cylinder;
    private float _newLightSourceRadius = 5f;
    private float _newLightSourceHeight = 10f;
    private float _newLightSourceRadiusStart = 5f;
    private float _newLightSourceRadiusEnd = 3f;
    private float _newLightSourceLength = 10f;
    private float _newLightSourceArcRadius = 20f;
    private int _newHybridSegmentCount = 1;
    private OgiveCurvatureDirection _newLightSourceOgiveCurvatureDirection = OgiveCurvatureDirection.Outward;
    private int _newLightSourceRayCount = 200;
    private float _newLightSourceTiltWeight = 0.1f;
    private float _newLightSourceTiltPointX;
    private float _newLightSourceTiltPointY;
    private float _newLightSourceTiltPointZ;
    private CollisionAlgorithmOption _selectedCollisionAlgorithm = CollisionAlgorithmOption.ClosestHitSequential;
    private string _lastCollisionDurationMs = "N/A";
    private string _lastSequentialCollisionDurationMs = "N/A";
    private string _lastParallelCollisionDurationMs = "N/A";
    private string _statusMessage = "Add objects, then click Run Collision.";
    private bool _isConsoleVisible = true;
    private bool _isNavigationCollapsed;
    private WorkspaceKind _selectedWorkspace = WorkspaceKind.Collision;
    private CollisionSceneViewModel? _subscribedScene;
    private float _selectedEditPositionX;
    private float _selectedEditPositionY;
    private float _selectedEditPositionZ;
    private float _selectedEditRotationX;
    private float _selectedEditRotationY;
    private float _selectedEditRotationZ;
    private float _selectedEditSizeX = 1f;
    private float _selectedEditSizeY = 1f;
    private float _selectedEditSizeZ = 1f;

    public MainWindowViewModel(SceneRenderSyncService renderSyncService, ProjectionRenderSyncService projectionRenderSyncService)
    {
        _renderSyncService = renderSyncService ?? throw new ArgumentNullException(nameof(renderSyncService));
        ArgumentNullException.ThrowIfNull(projectionRenderSyncService);
        AppLog = (Application.Current as App)?.AppLog ?? new ApplicationLogService();
        _sceneCollectionService = new SceneCollectionService();
        _sceneCollectionService.PropertyChanged += OnSceneCollectionPropertyChanged;
        _sceneCollectionService.SceneContentChanged += OnSceneContentChanged;
        // Generated axisymmetric sources live in LightSources.
        // Projected light sources live in ProjectedLightSources and preserve exact rays.

        AnnotationWorkspace = new AnnotationWorkspaceViewModel(_sceneCollectionService);
        ProjectionWorkspace = new ProjectionWorkspaceViewModel(_sceneCollectionService, projectionRenderSyncService, applicationLogService: AppLog);
        GraphicMasterWorkspace = new GraphicMasterViewModel(_sceneCollectionService, _completedSourceStore);
        SourceCompletionWorkspace = new SourceCompletionWorkspaceViewModel(_sceneCollectionService, _completedSourceStore, ProjectionWorkspace, applicationLogService: AppLog);
        CollisionScenes = CollectionViewSource.GetDefaultView(_sceneCollectionService.Scenes);
        CollisionScenes.Filter = item => item is CollisionSceneViewModel scene && !scene.IsProjectionOnly;

        CreateSceneCommand = new RelayCommand(CreateScene);
        DeleteSelectedSceneCommand = new RelayCommand(DeleteSelectedScene, () => SelectedScene is not null);
        AddPrismCommand = new RelayCommand(AddPrism, () => SelectedScene is not null);
        AddPrismArrayCommand = new RelayCommand(AddPrismArray, () => SelectedScene is not null);
        ApplySelectedPrismChangesCommand = new RelayCommand(ApplySelectedPrismChanges, () => SelectedScene is not null && SelectedPrism is not null);
        ApplySelectedSourceChangesCommand = new RelayCommand(ApplySelectedSourceChanges, () => SelectedScene is not null && IsSelectedSourceEditable);
        ApplySelectedObjectChangesCommand = new RelayCommand(ApplySelectedObjectChanges, CanApplySelectedObjectChanges);
        AddRayCommand = new RelayCommand(AddRay, () => SelectedScene is not null);
        AddLightSourceCommand = new RelayCommand(AddLightSource, () => SelectedScene is not null);
        AddHybridSegmentCommand = new RelayCommand(AddHybridSegment);
        RemoveSelectedHybridSegmentCommand = new RelayCommand(RemoveSelectedHybridSegment, () => SelectedNewHybridSegment is not null);
        RemoveSelectedPrismCommand = new RelayCommand(RemoveSelectedPrism, () => SelectedPrism is not null);
        RemoveAllPrismsCommand = new RelayCommand(RemoveAllPrisms, () => Prisms.Count > 0);
        RemoveSelectedRayCommand = new RelayCommand(RemoveSelectedRay, () => SelectedRay is not null);
        RemoveAllRaysCommand = new RelayCommand(RemoveAllRays, () => Rays.Count > 0);
        RemoveSelectedLightSourceCommand = new RelayCommand(RemoveSelectedLightSource, () => SelectedLightSource is not null);
        RemoveSelectedProjectedLightSourceCommand = new RelayCommand(RemoveSelectedProjectedLightSource, () => SelectedProjectedLightSource is not null);
        RunCollisionCommand = new RelayCommand(RunCollision, () => SelectedScene is not null);
        ExportHitPointsCsvCommand = new RelayCommand(ExportHitPointsCsv);
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
        ShowGraphicMasterWorkspaceCommand = new RelayCommand(() => SelectedWorkspace = WorkspaceKind.GraphicMaster);
        ShowSourceCompletionWorkspaceCommand = new RelayCommand(() => SelectedWorkspace = WorkspaceKind.SourceCompletion);
        ClearConsoleCommand = new RelayCommand(() => AppLog.Clear());
        CopyConsoleCommand = new RelayCommand(CopyConsoleToClipboard);
        ShowConsoleCommand = new RelayCommand(() => IsConsoleVisible = true, () => !IsConsoleVisible);
        HideConsoleCommand = new RelayCommand(() => IsConsoleVisible = false, () => IsConsoleVisible);
        ToggleConsoleCommand = new RelayCommand(() => IsConsoleVisible = !IsConsoleVisible);

        if (NewHybridSegments.Count == 0)
        {
            AddHybridSegment();
        }
        CreateScene();
        AppLog.LogInfo("Application started.", nameof(MainWindowViewModel));
        RefreshViewport(false);
    }

    public string Title => "Laser Collision in 3D Objects";

    public AnnotationWorkspaceViewModel AnnotationWorkspace { get; }
    public ProjectionWorkspaceViewModel ProjectionWorkspace { get; }
    public GraphicMasterViewModel GraphicMasterWorkspace { get; }
    public SourceCompletionWorkspaceViewModel SourceCompletionWorkspace { get; }
    public ICollectionView CollisionScenes { get; }
    public ApplicationLogService AppLog { get; }
    public ObservableCollection<ApplicationLogEntry> ConsoleEntries => AppLog.Entries;

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
                SetStatus($"Selected scene '{value.Name}'.");
            }

            RefreshSceneBindingsAndViewport();
        }
    }

    public ObservableCollection<PrismItemViewModel> Prisms => SelectedScene?.Prisms ?? EmptyPrisms;
    public ObservableCollection<RayItemViewModel> Rays => SelectedScene?.Rays ?? EmptyRays;
    public ObservableCollection<CylindricalLightSourceItemViewModel> LightSources => SelectedScene?.LightSources ?? EmptyLightSources;
    public ObservableCollection<ProjectedLightSourceItemViewModel> ProjectedLightSources => SelectedScene?.ProjectedLightSources ?? EmptyProjectedLightSources;
    public ObservableCollection<HitResultItemViewModel> HitResults => SelectedScene?.HitResults ?? EmptyHitResults;

    public PrismArrayPlacementMode[] PrismArrayPlacementModes { get; } = Enum.GetValues<PrismArrayPlacementMode>();
    public CollisionAlgorithmOption[] CollisionAlgorithms { get; } = Enum.GetValues<CollisionAlgorithmOption>();
    public AxisymmetricSourceKind[] LightSourceKinds { get; } = Enum.GetValues<AxisymmetricSourceKind>();
    public OgiveCurvatureDirection[] OgiveCurvatureDirections { get; } = Enum.GetValues<OgiveCurvatureDirection>();
    public HybridAxisymmetricSourceSegmentKind[] HybridSegmentKinds { get; } = Enum.GetValues<HybridAxisymmetricSourceSegmentKind>();
    public ObservableCollection<HybridSourceSegmentItemViewModel> NewHybridSegments { get; } = new();

    private HybridSourceSegmentItemViewModel? _selectedNewHybridSegment;
    public HybridSourceSegmentItemViewModel? SelectedNewHybridSegment
    {
        get => _selectedNewHybridSegment;
        set
        {
            if (SetProperty(ref _selectedNewHybridSegment, value) && RemoveSelectedHybridSegmentCommand is RelayCommand removeHybridSegmentCommand)
            {
                removeHybridSegmentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand CreateSceneCommand { get; }
    public ICommand DeleteSelectedSceneCommand { get; }
    public ICommand AddPrismCommand { get; }
    public ICommand AddPrismArrayCommand { get; }
    public ICommand ApplySelectedPrismChangesCommand { get; }
    public ICommand ApplySelectedSourceChangesCommand { get; }
    public ICommand ApplySelectedObjectChangesCommand { get; }
    public ICommand AddRayCommand { get; }
    public ICommand AddLightSourceCommand { get; }
    public ICommand AddHybridSegmentCommand { get; }
    public ICommand RemoveSelectedHybridSegmentCommand { get; }
    public ICommand RemoveSelectedPrismCommand { get; }
    public ICommand RemoveAllPrismsCommand { get; }
    public ICommand RemoveSelectedRayCommand { get; }
    public ICommand RemoveAllRaysCommand { get; }
    public ICommand RemoveSelectedLightSourceCommand { get; }
    public ICommand RemoveSelectedProjectedLightSourceCommand { get; }
    public ICommand RunCollisionCommand { get; }
    public ICommand ExportHitPointsCsvCommand { get; }
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
    public ICommand ShowGraphicMasterWorkspaceCommand { get; }
    public ICommand ShowSourceCompletionWorkspaceCommand { get; }
    public ICommand ClearConsoleCommand { get; }
    public ICommand CopyConsoleCommand { get; }
    public ICommand ShowConsoleCommand { get; }
    public ICommand HideConsoleCommand { get; }
    public ICommand ToggleConsoleCommand { get; }

    public bool IsConsoleVisible
    {
        get => _isConsoleVisible;
        set
        {
            if (SetProperty(ref _isConsoleVisible, value))
            {
                RaisePropertyChanged(nameof(ConsoleVisibilityMenuText));
                RaiseConsoleCommandState();
            }
        }
    }

    public string ConsoleVisibilityMenuText => IsConsoleVisible ? "Hide Console" : "Show Console";

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
    public bool IsSelectedPrismEditable => SelectedPrism is not null;
    public bool IsSelectedSourceEditable => SelectedLightSource is not null || SelectedProjectedLightSource is not null;
    public bool HasNoPrismSelection => !IsSelectedPrismEditable;
    public bool HasNoSourceSelection => !IsSelectedSourceEditable;
    public bool HasEditableSelection => IsSelectedPrismEditable || IsSelectedSourceEditable;
    public bool HasNoEditableSelection => !HasEditableSelection;
    public string SelectedObjectEditorType => SelectedPrism is not null
        ? "Generated Prism"
        : SelectedLightSource is not null
            ? "Light Source"
            : SelectedProjectedLightSource is not null
                ? (SelectedProjectedLightSource.OriginKind == ProjectedLightSourceOriginKind.CompletedProjectionResult
                    ? "Completed Source"
                    : "Projected Light Source")
                : "None";

    public PrismItemViewModel? SelectedPrism
    {
        get => SelectedScene?.SelectedPrism;
        set
        {
            if (SelectedScene is null)
            {
                return;
            }

            if (!Equals(SelectedScene.SelectedPrism, value))
            {
                SelectedScene.SelectedPrism = value;
            }
            if (value is not null)
            {
                if (SelectedScene.SelectedLightSource is not null) SelectedScene.SelectedLightSource = null;
                if (SelectedScene.SelectedProjectedLightSource is not null) SelectedScene.SelectedProjectedLightSource = null;
                if (SelectedScene.SelectedRay is not null) SelectedScene.SelectedRay = null;
                LoadPrismIntoEditor(value);
            }

            RaiseCanExecuteChanges();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSelectedPrismEditable));
            RaisePropertyChanged(nameof(IsSelectedSourceEditable));
            RaisePropertyChanged(nameof(HasNoPrismSelection));
            RaisePropertyChanged(nameof(HasNoSourceSelection));
            RaisePropertyChanged(nameof(HasEditableSelection));
            RaisePropertyChanged(nameof(HasNoEditableSelection));
            RaisePropertyChanged(nameof(SelectedObjectEditorType));
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
            RaisePropertyChanged(nameof(IsSelectedPrismEditable));
            RaisePropertyChanged(nameof(IsSelectedSourceEditable));
            RaisePropertyChanged(nameof(HasNoPrismSelection));
            RaisePropertyChanged(nameof(HasNoSourceSelection));
            RaisePropertyChanged(nameof(HasEditableSelection));
        }
    }

    public CylindricalLightSourceItemViewModel? SelectedLightSource
    {
        get => SelectedScene?.SelectedLightSource;
        set
        {
            if (SelectedScene is null)
            {
                return;
            }

            if (!Equals(SelectedScene.SelectedLightSource, value))
            {
                SelectedScene.SelectedLightSource = value;
            }
            if (value is not null)
            {
                if (SelectedScene.SelectedPrism is not null) SelectedScene.SelectedPrism = null;
                if (SelectedScene.SelectedProjectedLightSource is not null) SelectedScene.SelectedProjectedLightSource = null;
                if (SelectedScene.SelectedRay is not null) SelectedScene.SelectedRay = null;
                LoadLightSourceIntoEditor(value);
            }

            RaiseCanExecuteChanges();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSelectedPrismEditable));
            RaisePropertyChanged(nameof(IsSelectedSourceEditable));
            RaisePropertyChanged(nameof(HasNoPrismSelection));
            RaisePropertyChanged(nameof(HasNoSourceSelection));
            RaisePropertyChanged(nameof(HasEditableSelection));
            RaisePropertyChanged(nameof(HasNoEditableSelection));
            RaisePropertyChanged(nameof(SelectedObjectEditorType));
        }
    }

    public ProjectedLightSourceItemViewModel? SelectedProjectedLightSource
    {
        get => SelectedScene?.SelectedProjectedLightSource;
        set
        {
            if (SelectedScene is null)
            {
                return;
            }

            if (!Equals(SelectedScene.SelectedProjectedLightSource, value))
            {
                SelectedScene.SelectedProjectedLightSource = value;
            }
            if (value is not null)
            {
                if (SelectedScene.SelectedPrism is not null) SelectedScene.SelectedPrism = null;
                if (SelectedScene.SelectedLightSource is not null) SelectedScene.SelectedLightSource = null;
                if (SelectedScene.SelectedRay is not null) SelectedScene.SelectedRay = null;
                LoadProjectedLightSourceIntoEditor(value);
            }
            RaiseCanExecuteChanges();
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsSelectedPrismEditable));
            RaisePropertyChanged(nameof(IsSelectedSourceEditable));
            RaisePropertyChanged(nameof(HasNoPrismSelection));
            RaisePropertyChanged(nameof(HasNoSourceSelection));
            RaisePropertyChanged(nameof(HasEditableSelection));
            RaisePropertyChanged(nameof(HasNoEditableSelection));
            RaisePropertyChanged(nameof(SelectedObjectEditorType));
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
    public float SelectedEditPositionX { get => _selectedEditPositionX; set => SetProperty(ref _selectedEditPositionX, value); }
    public float SelectedEditPositionY { get => _selectedEditPositionY; set => SetProperty(ref _selectedEditPositionY, value); }
    public float SelectedEditPositionZ { get => _selectedEditPositionZ; set => SetProperty(ref _selectedEditPositionZ, value); }
    public float SelectedEditRotationX { get => _selectedEditRotationX; set => SetProperty(ref _selectedEditRotationX, value); }
    public float SelectedEditRotationY { get => _selectedEditRotationY; set => SetProperty(ref _selectedEditRotationY, value); }
    public float SelectedEditRotationZ { get => _selectedEditRotationZ; set => SetProperty(ref _selectedEditRotationZ, value); }
    public float SelectedEditSizeX { get => _selectedEditSizeX; set => SetProperty(ref _selectedEditSizeX, value); }
    public float SelectedEditSizeY { get => _selectedEditSizeY; set => SetProperty(ref _selectedEditSizeY, value); }
    public float SelectedEditSizeZ { get => _selectedEditSizeZ; set => SetProperty(ref _selectedEditSizeZ, value); }
    public float SelectedPrismEditPositionX { get => SelectedEditPositionX; set => SelectedEditPositionX = value; }
    public float SelectedPrismEditPositionY { get => SelectedEditPositionY; set => SelectedEditPositionY = value; }
    public float SelectedPrismEditPositionZ { get => SelectedEditPositionZ; set => SelectedEditPositionZ = value; }
    public float SelectedPrismEditRotationX { get => SelectedEditRotationX; set => SelectedEditRotationX = value; }
    public float SelectedPrismEditRotationY { get => SelectedEditRotationY; set => SelectedEditRotationY = value; }
    public float SelectedPrismEditRotationZ { get => SelectedEditRotationZ; set => SelectedEditRotationZ = value; }
    public float SelectedPrismEditSizeX { get => SelectedEditSizeX; set => SelectedEditSizeX = value; }
    public float SelectedPrismEditSizeY { get => SelectedEditSizeY; set => SelectedEditSizeY = value; }
    public float SelectedPrismEditSizeZ { get => SelectedEditSizeZ; set => SelectedEditSizeZ = value; }
    public float SelectedSourceEditPositionX { get => SelectedEditPositionX; set => SelectedEditPositionX = value; }
    public float SelectedSourceEditPositionY { get => SelectedEditPositionY; set => SelectedEditPositionY = value; }
    public float SelectedSourceEditPositionZ { get => SelectedEditPositionZ; set => SelectedEditPositionZ = value; }
    public float SelectedSourceEditRotationX { get => SelectedEditRotationX; set => SelectedEditRotationX = value; }
    public float SelectedSourceEditRotationY { get => SelectedEditRotationY; set => SelectedEditRotationY = value; }
    public float SelectedSourceEditRotationZ { get => SelectedEditRotationZ; set => SelectedEditRotationZ = value; }
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
    public AxisymmetricSourceKind NewLightSourceKind
    {
        get => _newLightSourceKind;
        set
        {
            if (SetProperty(ref _newLightSourceKind, value))
            {
                RaisePropertyChanged(nameof(IsNewLightSourceCylinder));
                RaisePropertyChanged(nameof(IsNewLightSourceConicalFrustum));
                RaisePropertyChanged(nameof(IsNewLightSourceCircularOgive));
                RaisePropertyChanged(nameof(IsNewLightSourceHybrid));
            }
        }
    }
    public float NewLightSourcePosX { get; set; }
    public float NewLightSourcePosY { get; set; }
    public float NewLightSourcePosZ { get; set; }
    public float NewLightSourceRotX { get; set; }
    public float NewLightSourceRotY { get; set; }
    public float NewLightSourceRotZ { get; set; }
    public float NewLightSourceRadius { get => _newLightSourceRadius; set => SetProperty(ref _newLightSourceRadius, value); }
    public float NewLightSourceHeight { get => _newLightSourceHeight; set => SetProperty(ref _newLightSourceHeight, value); }
    public float NewLightSourceRadiusStart { get => _newLightSourceRadiusStart; set => SetProperty(ref _newLightSourceRadiusStart, value); }
    public float NewLightSourceRadiusEnd { get => _newLightSourceRadiusEnd; set => SetProperty(ref _newLightSourceRadiusEnd, value); }
    public float NewLightSourceLength { get => _newLightSourceLength; set => SetProperty(ref _newLightSourceLength, value); }
    public float NewLightSourceArcRadius { get => _newLightSourceArcRadius; set => SetProperty(ref _newLightSourceArcRadius, value); }
    public int NewHybridSegmentCount { get => _newHybridSegmentCount; set => SetProperty(ref _newHybridSegmentCount, value); }
    public OgiveCurvatureDirection NewLightSourceOgiveCurvatureDirection { get => _newLightSourceOgiveCurvatureDirection; set => SetProperty(ref _newLightSourceOgiveCurvatureDirection, value); }
    public int NewLightSourceRayCount { get => _newLightSourceRayCount; set => SetProperty(ref _newLightSourceRayCount, value); }
    public float NewLightSourceTiltWeight { get => _newLightSourceTiltWeight; set => SetProperty(ref _newLightSourceTiltWeight, value); }
    public float NewLightSourceTiltPointX { get => _newLightSourceTiltPointX; set => SetProperty(ref _newLightSourceTiltPointX, value); }
    public float NewLightSourceTiltPointY { get => _newLightSourceTiltPointY; set => SetProperty(ref _newLightSourceTiltPointY, value); }
    public float NewLightSourceTiltPointZ { get => _newLightSourceTiltPointZ; set => SetProperty(ref _newLightSourceTiltPointZ, value); }

    public bool IsNewLightSourceCylinder => NewLightSourceKind == AxisymmetricSourceKind.Cylinder;
    public bool IsNewLightSourceConicalFrustum => NewLightSourceKind == AxisymmetricSourceKind.ConicalFrustum;
    public bool IsNewLightSourceCircularOgive => NewLightSourceKind == AxisymmetricSourceKind.CircularOgive;
    public bool IsNewLightSourceHybrid => NewLightSourceKind == AxisymmetricSourceKind.Hybrid;
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
        SetStatus($"Created scene '{scene.Name}'.", ApplicationLogLevel.Success);
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void DeleteSelectedScene()
    {
        if (SelectedScene is null)
        {
            SetStatus("Select a scene to delete.", ApplicationLogLevel.Warning);
            return;
        }

        var deletedName = SelectedScene.Name;
        _sceneCollectionService.RemoveScene(SelectedScene);
        SetStatus(Scenes.Count == 0
            ? $"Deleted '{deletedName}'. Workspace is empty."
            : $"Deleted '{deletedName}'.",
            ApplicationLogLevel.Success);

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
            SetStatus(error, ApplicationLogLevel.Warning);
            return;
        }

        scene.Prisms.Add(CreatePrismViewModel(
            string.IsNullOrWhiteSpace(NewPrismName) ? $"Prism {scene.Prisms.Count + 1}" : NewPrismName,
            new Vector3(NewPrismPosX, NewPrismPosY, NewPrismPosZ),
            Quaternion.Identity));

        SelectedPrism = scene.Prisms.Last();
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
            SetStatus(error, ApplicationLogLevel.Warning);
            return;
        }

        if (!ValidatePrismArrayInputs(SelectedPrismArrayPlacementMode, NewPrismArrayCount, NewPrismArrayRadius, NewPrismArrayLength, out error))
        {
            SetStatus(error, ApplicationLogLevel.Warning);
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

        SelectedPrism = created.LastOrDefault();
        NewPrismName = $"Prism {scene.Prisms.Count + 1}";
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        SetStatus($"Added {created.Count} prisms in a {SelectedPrismArrayPlacementMode} array around the world origin with global-axis-aligned default frames.", ApplicationLogLevel.Success);
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
            SetStatus(error, ApplicationLogLevel.Warning);
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


    private void AddHybridSegment()
    {
        var previous = NewHybridSegments.LastOrDefault();
        var radius = previous?.RadiusEnd ?? NewLightSourceRadiusStart;
        var segment = new HybridSourceSegmentItemViewModel
        {
            SegmentIndex = NewHybridSegments.Count + 1,
            IsRadiusStartEditable = NewHybridSegments.Count == 0,
            RadiusStart = radius,
            RadiusEnd = radius,
        };
        AttachHybridSegment(segment);
        NewHybridSegments.Add(segment);

        SynchronizeHybridSegmentContinuity();
        SelectedNewHybridSegment = NewHybridSegments.LastOrDefault();
        SetStatus($"Hybrid segment {NewHybridSegments.Count} added.", ApplicationLogLevel.Trace);
    }

    private void RemoveSelectedHybridSegment()
    {
        if (SelectedNewHybridSegment is null)
        {
            SetStatus("Select a hybrid segment to remove.", ApplicationLogLevel.Warning);
            return;
        }

        DetachHybridSegment(SelectedNewHybridSegment);
        NewHybridSegments.Remove(SelectedNewHybridSegment);
        if (NewHybridSegments.Count == 0)
        {
            SetStatus("Hybrid source requires at least one segment.", ApplicationLogLevel.Warning);
            AddHybridSegment();
            return;
        }

        SynchronizeHybridSegmentContinuity();
        SelectedNewHybridSegment = NewHybridSegments.LastOrDefault();
        SetStatus("Selected hybrid segment removed.", ApplicationLogLevel.Trace);
    }

    private void SynchronizeHybridSegmentContinuity()
    {
        for (var i = 0; i < NewHybridSegments.Count; i++)
        {
            var segment = NewHybridSegments[i];
            segment.SegmentIndex = i + 1;
            segment.IsRadiusStartEditable = i == 0;
            if (i > 0)
            {
                segment.RadiusStart = NewHybridSegments[i - 1].RadiusEnd;
            }

            if (segment.SegmentKind == HybridAxisymmetricSourceSegmentKind.Cylinder)
            {
                if (segment.RadiusEnd != segment.RadiusStart)
                {
                    segment.RadiusEnd = segment.RadiusStart;
                }
            }
        }
    }

    private void AttachHybridSegment(HybridSourceSegmentItemViewModel segment)
    {
        segment.PropertyChanged += OnHybridSegmentPropertyChanged;
    }

    private void DetachHybridSegment(HybridSourceSegmentItemViewModel segment)
    {
        segment.PropertyChanged -= OnHybridSegmentPropertyChanged;
    }

    private void OnHybridSegmentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HybridSourceSegmentItemViewModel.SegmentKind)
            or nameof(HybridSourceSegmentItemViewModel.Radius)
            or nameof(HybridSourceSegmentItemViewModel.RadiusStart)
            or nameof(HybridSourceSegmentItemViewModel.RadiusEnd))
        {
            SynchronizeHybridSegmentContinuity();
        }
    }

    private static void SynchronizeHybridSegmentContinuity(IList<HybridSourceSegmentItemViewModel> segments)
    {
        for (var i = 1; i < segments.Count; i++)
        {
            segments[i].RadiusStart = segments[i - 1].RadiusEnd;
            segments[i].IsRadiusStartEditable = false;
            segments[i].SegmentIndex = i + 1;
            if (segments[i].SegmentKind == HybridAxisymmetricSourceSegmentKind.Cylinder)
            {
                segments[i].RadiusEnd = segments[i].RadiusStart;
            }
        }

        if (segments.Count > 0)
        {
            segments[0].IsRadiusStartEditable = true;
            segments[0].SegmentIndex = 1;
        }
    }

    private void AddLightSource()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null)
        {
            return;
        }

        if (NewLightSourceKind == AxisymmetricSourceKind.Hybrid)
        {
            SynchronizeHybridSegmentContinuity();
            if (NewHybridSegments.Count == 0)
            {
                SetStatus("Hybrid source requires at least one segment.", ApplicationLogLevel.Warning);
                return;
            }
        }

        if (!ValidateLightSourceInputs(NewLightSourceKind, NewLightSourceRadius, NewLightSourceHeight, NewLightSourceRadiusStart, NewLightSourceRadiusEnd, NewLightSourceLength, NewLightSourceArcRadius, NewLightSourceRayCount, NewLightSourceTiltWeight, out var error))
        {
            SetStatus(error, ApplicationLogLevel.Warning);
            return;
        }

        scene.LightSources.Add(new CylindricalLightSourceItemViewModel
        {
            Name = string.IsNullOrWhiteSpace(NewLightSourceName) ? $"Light Source {scene.LightSources.Count + 1}" : NewLightSourceName,
            SourceKind = NewLightSourceKind,
            PositionX = NewLightSourcePosX,
            PositionY = NewLightSourcePosY,
            PositionZ = NewLightSourcePosZ,
            RotationX = NewLightSourceRotX,
            RotationY = NewLightSourceRotY,
            RotationZ = NewLightSourceRotZ,
            Radius = NewLightSourceRadius,
            Height = NewLightSourceHeight,
            RadiusStart = NewLightSourceRadiusStart,
            RadiusEnd = NewLightSourceRadiusEnd,
            Length = NewLightSourceLength,
            ArcRadius = NewLightSourceArcRadius,
            OgiveCurvatureDirection = NewLightSourceOgiveCurvatureDirection,
            RayCount = NewLightSourceRayCount,
            TiltWeight = NewLightSourceTiltWeight,
            TiltPointX = NewLightSourceTiltPointX,
            TiltPointY = NewLightSourceTiltPointY,
            TiltPointZ = NewLightSourceTiltPointZ,
            BaseOrientation = Quaternion.Identity,
        });

        if (NewLightSourceKind == AxisymmetricSourceKind.Hybrid)
        {
            var added = scene.LightSources.Last();
            foreach (var segment in NewHybridSegments)
            {
                added.HybridSegments.Add(new HybridSourceSegmentItemViewModel
                {
                    SegmentIndex = segment.SegmentIndex,
                    SegmentKind = segment.SegmentKind,
                    Length = segment.Length,
                    RadiusStart = segment.RadiusStart,
                    RadiusEnd = segment.RadiusEnd,
                    ArcRadius = segment.ArcRadius,
                    OgiveCurvatureDirection = segment.OgiveCurvatureDirection,
                    IsRadiusStartEditable = segment.IsRadiusStartEditable,
                });
            }
        }

        SelectedLightSource = scene.LightSources.Last();
        NewLightSourceName = $"Light Source {scene.LightSources.Count + 1}";
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        SetStatus($"Added {NewLightSourceKind} light source.");
    }

    private void RemoveSelectedPrism()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene?.SelectedPrism is null)
        {
            SetStatus("Select a prism to remove.", ApplicationLogLevel.Warning);
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
            SetStatus("There are no prisms to delete.", ApplicationLogLevel.Warning);
            return;
        }

        var deleted = scene.Prisms.Count;
        scene.Prisms.Clear();
        scene.SelectedPrism = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        SetStatus($"Deleted {deleted} prisms.", ApplicationLogLevel.Success);
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
            SetStatus("There are no rays to delete.", ApplicationLogLevel.Warning);
            return;
        }

        var deleted = scene.Rays.Count;
        scene.Rays.Clear();
        scene.SelectedRay = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        SetStatus($"Deleted {deleted} rays.", ApplicationLogLevel.Success);
    }

    private void RemoveSelectedRay()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene?.SelectedRay is null)
        {
            SetStatus("Select a ray to remove.", ApplicationLogLevel.Warning);
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
            SetStatus("Select a light source to remove.", ApplicationLogLevel.Warning);
            return;
        }

        scene.LightSources.Remove(scene.SelectedLightSource);
        scene.SelectedLightSource = null;
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void RemoveSelectedProjectedLightSource()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene?.SelectedProjectedLightSource is null)
        {
            SetStatus("Select a projected light source to remove.", ApplicationLogLevel.Warning);
            return;
        }

        var removed = scene.SelectedProjectedLightSource;
        var removedName = removed.Name;
        scene.ProjectedLightSources.Remove(removed);
        scene.SelectedProjectedLightSource = null;
        ClearCollisionResults(scene);
        RaiseCanExecuteChanges();
        RefreshViewport(false);
        SetStatus($"Removed projected light source '{removedName}' from collision scene '{scene.Name}'.", ApplicationLogLevel.Success);
    }

    private void RunCollision()
    {
        if (SelectedScene is null)
        {
            SetStatus("Create or select a scene first.", ApplicationLogLevel.Warning);
            return;
        }

        if (!ValidateAllSceneItems(out var error))
        {
            SetStatus(error, ApplicationLogLevel.Warning);
            return;
        }

        RefreshViewport(true);
    }

    private void RegenerateLightSourceRays()
    {
        if (SelectedScene is null)
        {
            SetStatus("Create or select a scene first.", ApplicationLogLevel.Warning);
            return;
        }

        if (!ValidateAllSceneItems(out var error))
        {
            SetStatus(error, ApplicationLogLevel.Warning);
            return;
        }

        RefreshViewport(false);
        SetStatus("Generated rays refreshed from axisymmetric light sources.");
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
            SourceKind = AxisymmetricSourceKind.Cylinder,
            PositionX = -10,
            PositionY = 0,
            PositionZ = 0,
            Radius = 4,
            Height = 10,
            RadiusStart = 4,
            RadiusEnd = 4,
            Length = 10,
            ArcRadius = 20,
            OgiveCurvatureDirection = OgiveCurvatureDirection.Outward,
            RayCount = 120,
            TiltWeight = 0.1f,
            TiltPointX = 0f,
            TiltPointY = 0f,
            TiltPointZ = 0f,
            BaseOrientation = Quaternion.Identity,
        });

        if (NewLightSourceKind == AxisymmetricSourceKind.Hybrid)
        {
            var added = scene.LightSources.Last();
            foreach (var segment in NewHybridSegments)
            {
                added.HybridSegments.Add(new HybridSourceSegmentItemViewModel
                {
                    SegmentIndex = segment.SegmentIndex,
                    SegmentKind = segment.SegmentKind,
                    Length = segment.Length,
                    RadiusStart = segment.RadiusStart,
                    RadiusEnd = segment.RadiusEnd,
                    ArcRadius = segment.ArcRadius,
                    OgiveCurvatureDirection = segment.OgiveCurvatureDirection,
                    IsRadiusStartEditable = segment.IsRadiusStartEditable,
                });
            }
        }

        scene.SelectedPrism = null;
        scene.SelectedRay = null;
        scene.SelectedLightSource = null;

        RefreshViewport(true);
        SetStatus("Demo scene reset with manual and generated rays.", ApplicationLogLevel.Success);
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
            var projectedLightSources = scene?.ProjectedLightSources ?? EmptyProjectedLightSources;
            var holes = scene?.HolePoints ?? EmptyHoles;
            var projectionResult = scene?.ProjectionState.SelectedResult;
            var sceneName = scene?.Name ?? "Scene";

            var sceneSyncResult = _renderSyncService.SyncScene(prisms, lightSources, rays, projectedLightSources, holes, projectionResult, sceneName, runCollision, SelectedCollisionAlgorithm);
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
                var projectedRaysTested = projectedLightSources.Sum(source => source.Rays.Count);
                _lastCollisionHitPointRecords = sceneSyncResult.HitPointRecords;
                var elapsedMs = sceneSyncResult.CollisionDuration.TotalMilliseconds;
                LastCollisionDurationMs = $"{elapsedMs:F3}";
                var projectedHits = sceneSyncResult.HitPointRecords.Count(record => record.SourceType == CollisionRaySourceType.ProjectionResult);

                if (sceneSyncResult.CollisionAlgorithm == CollisionAlgorithmOption.ClosestHitSequential)
                {
                    LastSequentialCollisionDurationMs = LastCollisionDurationMs;
                }
                else if (sceneSyncResult.CollisionAlgorithm == CollisionAlgorithmOption.ClosestHitParallel)
                {
                    LastParallelCollisionDurationMs = LastCollisionDurationMs;
                }

                SetStatus($"Collision run complete ({SelectedCollisionAlgorithm}) in {elapsedMs:F3} ms. Hits: {rows.Count(r => r.HasHit)}/{rows.Count}.", ApplicationLogLevel.Success);
                AppLog.LogInfo($"Collision: {projectedRaysTested} projected rays tested, {projectedHits} hits detected.", nameof(MainWindowViewModel));
            }
            else
            {
                SetStatus(scene is null
                    ? "No scene selected. Create a scene to begin."
                    : $"Scene refreshed. Manual rays: {rays.Count}, generated rays: {lightSources.Sum(s => Math.Max(0, s.RayCount))}.",
                    ApplicationLogLevel.Trace);
            }
        }
        catch (ArgumentException ex)
        {
            SetStatus($"Please check values: {ex.Message}", ApplicationLogLevel.Warning, ex);
            SelectedScene?.HitResults.Clear();
        }
    }

    private void ExportHitPointsCsv()
    {
        if (_lastCollisionHitPointRecords.Count == 0)
        {
            SetStatus("Run collision first to export hit points.", ApplicationLogLevel.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
            FileName = "collision-hit-points.csv",
        };

        if (dialog.ShowDialog() != true)
        {
            SetStatus("Export canceled.");
            return;
        }

        _collisionHitPointCsvExportService.Export(dialog.FileName, _lastCollisionHitPointRecords);
        SetStatus($"Exported {_lastCollisionHitPointRecords.Count} collision hit points to '{dialog.FileName}'.", ApplicationLogLevel.Success);
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
            if (!ValidateLightSourceInputs(source.SourceKind, source.Radius, source.Height, source.RadiusStart, source.RadiusEnd, source.Length, source.ArcRadius, source.RayCount, source.TiltWeight, out error))
            {
                error = $"Light source {i + 1} invalid. {error}";
                return false;
            }

            if (source.SourceKind == AxisymmetricSourceKind.Hybrid)
            {
                if (source.HybridSegments.Count == 0)
                {
                    error = $"Light source {i + 1} invalid. Hybrid source requires at least one segment.";
                    return false;
                }

                SynchronizeHybridSegmentContinuity(source.HybridSegments);
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

    private static bool ValidateLightSourceInputs(
        AxisymmetricSourceKind sourceKind,
        float radius,
        float height,
        float radiusStart,
        float radiusEnd,
        float length,
        float arcRadius,
        int rayCount,
        float tiltWeight,
        out string error)
    {
        if (sourceKind == AxisymmetricSourceKind.Cylinder)
        {
            if (radius <= 0 || height <= 0)
            {
                error = "Light source radius and height must be positive.";
                return false;
            }
        }
        else
        {
            if (sourceKind == AxisymmetricSourceKind.Hybrid)
            {
                error = string.Empty;
            }
            else
            {
                if (radiusStart <= 0f || radiusEnd <= 0f || length <= 0f)
                {
                    error = "Light source R1, R2, and length must be positive.";
                    return false;
                }

                if (sourceKind == AxisymmetricSourceKind.CircularOgive && arcRadius <= 0f)
                {
                    error = "Circular ogive arc radius must be positive.";
                    return false;
                }
            }
        }

        if (rayCount <= 0)
        {
            error = "Light source RayCount must be greater than zero.";
            return false;
        }

        if (tiltWeight < 0f)
        {
            error = "Light source TiltWeight must be greater than or equal to zero.";
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

        SetStatus("Create or select a scene first.", ApplicationLogLevel.Warning);
        return null;
    }

    private void SetStatus(string message, ApplicationLogLevel level = ApplicationLogLevel.Info, Exception? exception = null)
    {
        StatusMessage = message;

        switch (level)
        {
            case ApplicationLogLevel.Trace:
                AppLog.LogTrace(message, nameof(MainWindowViewModel));
                break;
            case ApplicationLogLevel.Info:
                AppLog.LogInfo(message, nameof(MainWindowViewModel));
                break;
            case ApplicationLogLevel.Success:
                AppLog.LogSuccess(message, nameof(MainWindowViewModel));
                break;
            case ApplicationLogLevel.Warning:
                AppLog.LogWarning(message, nameof(MainWindowViewModel));
                break;
            case ApplicationLogLevel.Error:
                AppLog.LogError(message, exception, nameof(MainWindowViewModel));
                break;
        }
    }

    private void CopyConsoleToClipboard()
    {
        try
        {
            Clipboard.SetText(AppLog.CopyAllText());
            SetStatus("Console text copied to clipboard.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to copy console text: {ex.Message}", ApplicationLogLevel.Warning, ex);
        }
    }

    private void UpdateSceneCollectionSubscriptions(CollisionSceneViewModel? scene)
    {
        if (ReferenceEquals(_subscribedScene, scene))
        {
            return;
        }

        if (_subscribedScene is not null)
        {
            _subscribedScene.LightSources.CollectionChanged -= OnSceneLightSourcesCollectionChanged;
            _subscribedScene.ProjectedLightSources.CollectionChanged -= OnSceneProjectedLightSourcesCollectionChanged;
        }

        _subscribedScene = scene;

        if (_subscribedScene is not null)
        {
            _subscribedScene.LightSources.CollectionChanged += OnSceneLightSourcesCollectionChanged;
            _subscribedScene.ProjectedLightSources.CollectionChanged += OnSceneProjectedLightSourcesCollectionChanged;
        }
    }

    private void OnSceneLightSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshViewport(false);
    }

    private void OnSceneProjectedLightSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(ProjectedLightSources));
        RefreshViewport(false);
    }

    private void OnSceneCollectionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SceneCollectionService.SelectedScene))
        {
            RefreshSceneBindingsAndViewport();
        }
    }

    private void OnSceneContentChanged(object? sender, EventArgs e)
    {
        RefreshSceneBindingsAndViewport();
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

        try
        {
            SetStatus($"Saving project to '{dialog.FileName}'...");
            _projectPersistenceCoordinator.SaveProject(dialog.FileName, _sceneCollectionService, SelectedScene, AnnotationWorkspace, ProjectionWorkspace);
            SetStatus($"Project saved to '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to save project: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Loading project from '{dialog.FileName}'...");
            _projectPersistenceCoordinator.LoadProject(dialog.FileName, _sceneCollectionService, AnnotationWorkspace, ProjectionWorkspace);
            RefreshSceneBindingsAndViewport();
            SetStatus($"Project loaded from '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to load project: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Saving collision tab state to '{dialog.FileName}'...");
            _projectPersistenceCoordinator.SaveCollisionTab(dialog.FileName, _sceneCollectionService, SelectedScene);
            SetStatus($"Collision tab state saved to '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to save collision tab state: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Loading collision tab state from '{dialog.FileName}'...");
            _projectPersistenceCoordinator.LoadCollisionTab(dialog.FileName, _sceneCollectionService);
            RefreshSceneBindingsAndViewport();
            SetStatus($"Collision tab state loaded from '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to load collision tab state: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Saving projection tab state to '{dialog.FileName}'...");
            _projectPersistenceCoordinator.SaveProjectionTab(dialog.FileName, _sceneCollectionService, ProjectionWorkspace);
            SetStatus($"Projection tab state saved to '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to save projection tab state: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Loading projection tab state from '{dialog.FileName}'...");
            _projectPersistenceCoordinator.LoadProjectionTab(dialog.FileName, _sceneCollectionService, ProjectionWorkspace);
            RefreshSceneBindingsAndViewport();
            SetStatus($"Projection tab state loaded from '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to load projection tab state: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Saving annotation tab state to '{dialog.FileName}'...");
            _projectPersistenceCoordinator.SaveAnnotationTab(dialog.FileName, AnnotationWorkspace);
            SetStatus($"Annotation tab state saved to '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to save annotation tab state: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
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

        try
        {
            SetStatus($"Loading annotation tab state from '{dialog.FileName}'...");
            _projectPersistenceCoordinator.LoadAnnotationTab(dialog.FileName, AnnotationWorkspace);
            SetStatus($"Annotation tab state loaded from '{dialog.FileName}'.", ApplicationLogLevel.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
        {
            SetStatus($"Failed to load annotation tab state: {ex.Message}", ApplicationLogLevel.Error, ex);
        }
    }

    private void RaiseConsoleCommandState()
    {
        if (ShowConsoleCommand is RelayCommand showConsoleCommand)
        {
            showConsoleCommand.RaiseCanExecuteChanged();
        }

        if (HideConsoleCommand is RelayCommand hideConsoleCommand)
        {
            hideConsoleCommand.RaiseCanExecuteChanged();
        }
    }

    private void RefreshSceneBindingsAndViewport()
    {
        CollisionScenes.Refresh();
        UpdateSceneCollectionSubscriptions(SelectedScene);
        if (SelectedScene?.SelectedPrism is not null)
        {
            LoadPrismIntoEditor(SelectedScene.SelectedPrism);
        }

        if (SelectedScene?.SelectedLightSource is not null)
        {
            LoadLightSourceIntoEditor(SelectedScene.SelectedLightSource);
        }

        RaisePropertyChanged(nameof(SelectedScene));
        RaisePropertyChanged(nameof(Prisms));
        RaisePropertyChanged(nameof(Rays));
        RaisePropertyChanged(nameof(LightSources));
        RaisePropertyChanged(nameof(ProjectedLightSources));
        RaisePropertyChanged(nameof(HitResults));
        RaisePropertyChanged(nameof(SelectedPrism));
        RaisePropertyChanged(nameof(SelectedRay));
        RaisePropertyChanged(nameof(SelectedLightSource));
        RaisePropertyChanged(nameof(SelectedProjectedLightSource));
        RaisePropertyChanged(nameof(IsSelectedPrismEditable));
        RaisePropertyChanged(nameof(IsSelectedSourceEditable));
        RaisePropertyChanged(nameof(HasEditableSelection));
        RaisePropertyChanged(nameof(HasNoEditableSelection));
        RaisePropertyChanged(nameof(SelectedObjectEditorType));
        RaiseCanExecuteChanges();
        RefreshViewport(false);
    }

    private void LoadPrismIntoEditor(PrismItemViewModel prism)
    {
        var effectiveOrientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(
            prism.BaseOrientation,
            prism.RotationX,
            prism.RotationY,
            prism.RotationZ);
        var (effectiveRotX, effectiveRotY, effectiveRotZ) = FrameOrientationBuilder.ToLocalEulerDegrees(effectiveOrientation);

        SelectedEditPositionX = prism.PositionX;
        SelectedEditPositionY = prism.PositionY;
        SelectedEditPositionZ = prism.PositionZ;
        SelectedEditRotationX = effectiveRotX;
        SelectedEditRotationY = effectiveRotY;
        SelectedEditRotationZ = effectiveRotZ;
        SelectedEditSizeX = prism.SizeX;
        SelectedEditSizeY = prism.SizeY;
        SelectedEditSizeZ = prism.SizeZ;
    }

    private void LoadLightSourceIntoEditor(CylindricalLightSourceItemViewModel source)
    {
        var effectiveOrientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(
            source.BaseOrientation,
            source.RotationX,
            source.RotationY,
            source.RotationZ);
        var (effectiveRotX, effectiveRotY, effectiveRotZ) = FrameOrientationBuilder.ToLocalEulerDegrees(effectiveOrientation);

        SelectedEditPositionX = source.PositionX;
        SelectedEditPositionY = source.PositionY;
        SelectedEditPositionZ = source.PositionZ;
        SelectedEditRotationX = effectiveRotX;
        SelectedEditRotationY = effectiveRotY;
        SelectedEditRotationZ = effectiveRotZ;
    }

    private void LoadProjectedLightSourceIntoEditor(ProjectedLightSourceItemViewModel source)
    {
        SelectedEditPositionX = (float)source.SourceFrame.Origin.X;
        SelectedEditPositionY = (float)source.SourceFrame.Origin.Y;
        SelectedEditPositionZ = (float)source.SourceFrame.Origin.Z;
        var (rx, ry, rz) = FrameOrientationBuilder.ToLocalEulerDegrees(source.BaseOrientation);
        SelectedEditRotationX = rx;
        SelectedEditRotationY = ry;
        SelectedEditRotationZ = rz;
    }

    private bool CanApplySelectedObjectChanges() => SelectedScene is not null && HasEditableSelection;

    private void ApplySelectedObjectChanges()
    {
        var scene = GetSelectedSceneOrSetStatus();
        if (scene is null) return;

        if (SelectedPrism is not null)
        {
            if (!ValidatePrismInputs(SelectedEditSizeX, SelectedEditSizeY, SelectedEditSizeZ, out var prismError))
            {
                SetStatus(prismError, ApplicationLogLevel.Warning);
                return;
            }

            SelectedPrism.PositionX = SelectedEditPositionX;
            SelectedPrism.PositionY = SelectedEditPositionY;
            SelectedPrism.PositionZ = SelectedEditPositionZ;
            SelectedPrism.RotationX = SelectedEditRotationX;
            SelectedPrism.RotationY = SelectedEditRotationY;
            SelectedPrism.RotationZ = SelectedEditRotationZ;
            SelectedPrism.SizeX = SelectedEditSizeX;
            SelectedPrism.SizeY = SelectedEditSizeY;
            SelectedPrism.SizeZ = SelectedEditSizeZ;
        }
        else if (SelectedLightSource is not null)
        {
            SelectedLightSource.PositionX = SelectedSourceEditPositionX;
            SelectedLightSource.PositionY = SelectedSourceEditPositionY;
            SelectedLightSource.PositionZ = SelectedSourceEditPositionZ;
            SelectedLightSource.RotationX = SelectedSourceEditRotationX;
            SelectedLightSource.RotationY = SelectedSourceEditRotationY;
            SelectedLightSource.RotationZ = SelectedSourceEditRotationZ;
        }
        else if (SelectedProjectedLightSource is not null)
        {
            var frame = SelectedProjectedLightSource.SourceFrame;
            var oldOrigin = new Vector3((float)frame.Origin.X, (float)frame.Origin.Y, (float)frame.Origin.Z);
            var oldRotation = SelectedProjectedLightSource.BaseOrientation;
            var newRotation = Quaternion.CreateFromYawPitchRoll(
                float.DegreesToRadians(SelectedSourceEditRotationY),
                float.DegreesToRadians(SelectedSourceEditRotationX),
                float.DegreesToRadians(SelectedSourceEditRotationZ));

            var deltaRotation = newRotation * Quaternion.Inverse(oldRotation);
            var newOrigin = new Vector3(SelectedSourceEditPositionX, SelectedSourceEditPositionY, SelectedSourceEditPositionZ);
            SelectedProjectedLightSource.SourceFrame = new PointSourceFrameState
            {
                Origin = new Point3(SelectedSourceEditPositionX, SelectedSourceEditPositionY, SelectedSourceEditPositionZ),
                AxisX = RotateFrameAxis(frame.AxisX, deltaRotation),
                AxisY = RotateFrameAxis(frame.AxisY, deltaRotation),
                AxisZ = RotateFrameAxis(frame.AxisZ, deltaRotation),
            };
            SelectedProjectedLightSource.BaseOrientation = newRotation;
            foreach (var projectionRay in SelectedProjectedLightSource.Rays)
            {
                var oldRayOrigin = projectionRay.Ray.Origin;
                var rotatedOffset = Vector3.Transform(oldRayOrigin - oldOrigin, deltaRotation);
                projectionRay.Ray.Origin = newOrigin + rotatedOffset;
                projectionRay.Ray.Direction = Vector3.Normalize(Vector3.TransformNormal(projectionRay.Ray.Direction, Matrix4x4.CreateFromQuaternion(deltaRotation)));
            }
        }

        ClearCollisionResults(scene);
        SetStatus("Selected object changes applied.", ApplicationLogLevel.Success);
        _sceneCollectionService.NotifySceneContentChanged();
        RefreshViewport(false);
    }

    private void ApplySelectedPrismChanges() => ApplySelectedObjectChanges();
    private void ApplySelectedSourceChanges() => ApplySelectedObjectChanges();

    private static Vector3D RotateFrameAxis(Vector3D axis, Quaternion rotation)
    {
        var rotated = Vector3.TransformNormal(new Vector3((float)axis.X, (float)axis.Y, (float)axis.Z), Matrix4x4.CreateFromQuaternion(rotation));
        if (rotated.LengthSquared() > 0f)
        {
            rotated = Vector3.Normalize(rotated);
        }

        return new Vector3D(rotated.X, rotated.Y, rotated.Z);
    }

    private void ClearCollisionResults(CollisionSceneViewModel scene)
    {
        scene.HitResults.Clear();
        _lastCollisionHitPointRecords = Array.Empty<CollisionHitPointRecord>();
        LastCollisionDurationMs = "N/A";
        RaisePropertyChanged(nameof(HitResults));
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
        if (ApplySelectedObjectChangesCommand is RelayCommand applyCommand)
        {
            applyCommand.RaiseCanExecuteChanged();
        }
        if (ApplySelectedPrismChangesCommand is RelayCommand applyPrismCommand)
        {
            applyPrismCommand.RaiseCanExecuteChanged();
        }
        if (ApplySelectedSourceChangesCommand is RelayCommand applySourceCommand)
        {
            applySourceCommand.RaiseCanExecuteChanged();
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

        if (RemoveSelectedProjectedLightSourceCommand is RelayCommand projectedLightCommand)
        {
            projectedLightCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedHybridSegmentCommand is RelayCommand removeHybridSegmentCommand)
        {
            removeHybridSegmentCommand.RaiseCanExecuteChanged();
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
