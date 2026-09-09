using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Persistence;
using LaserCollisionIn3DObjects.Domain.Projection;
using Microsoft.Win32;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;

public sealed class ProjectionWorkspaceViewModel : ObservableObject
{
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly ProjectionRenderSyncService _projectionRenderSyncService;
    private readonly ProjectionMethodRegistry _methodRegistry;
    private readonly ProjectionHitPointCsvImportService _projectionHitPointCsvImportService = new();
    private readonly ApplicationLogService? _applicationLogService;
    private readonly ProjectionResultToCollisionSourceService _projectionResultToCollisionSourceService;
    private ProjectionMethodOptionViewModel? _selectedMethod;
    private CollisionSceneViewModel? _selectedScene;
    private string _statusMessage = "Select a scene with holes to begin projection.";
    private string _newResultName = "Projection Result 1";
    private bool _isProjectionRunning;
    private bool _isBridgeBusy;
    private double _projectionProgressPercent;
    private string _projectionProgressMessage = string.Empty;
    private int _lastLoggedProgressBucket = -1;
    private AxisymmetricSourceKind _selectedAxisymmetricSourceKind = AxisymmetricSourceKind.Cylinder;
    private double _beamOriginX;
    private double _beamOriginY;
    private double _beamOriginZ;
    private double _sourceFrameXx = 1;
    private double _sourceFrameXy;
    private double _sourceFrameXz;
    private double _sourceFrameYx;
    private double _sourceFrameYy = 1;
    private double _sourceFrameYz;
    private double _geometryRadiusStart = 0.15d;
    private double _geometryRadiusEnd = 0.15d;
    private double _geometryLength = 0.30d;
    private double _geometryArcRadius = 20;
    private OgiveCurvatureDirection _geometryOgiveCurvatureDirection = OgiveCurvatureDirection.Outward;
    private HybridSourceSegmentItemViewModel? _selectedHybridSegment;
    private CollisionSceneViewModel? _selectedTargetCollisionScene;
    private int _leastSquaresMaxIterations = LeastSquaresAxisymmetricAlignmentSolverSettings.Default.MaxIterations;
    private double _leastSquaresConvergenceTolerance = LeastSquaresAxisymmetricAlignmentSolverSettings.Default.ConvergenceTolerance;
    private double _leastSquaresPointStepScale = LeastSquaresAxisymmetricAlignmentSolverSettings.Default.PointStepScale;
    private double _leastSquaresThetaStepScale = LeastSquaresAxisymmetricAlignmentSolverSettings.Default.ThetaStepScale;
    private double _leastSquaresLambdaStepScale = LeastSquaresAxisymmetricAlignmentSolverSettings.Default.LambdaStepScale;
    private bool _isApplyingWorkspaceState;
    private bool _showPanels = true;
    private bool _showMeasuredCorners = true;
    private bool _includeNaturalPoints = true;
    private ProjectionGeometrySnapshot _appliedGeometry = null!;

    public ProjectionWorkspaceViewModel(
        SceneCollectionService sceneCollectionService,
        ProjectionRenderSyncService projectionRenderSyncService,
        ProjectionMethodRegistry? methodRegistry = null,
        ApplicationLogService? applicationLogService = null,
        ProjectionResultToCollisionSourceService? projectionResultToCollisionSourceService = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _projectionRenderSyncService = projectionRenderSyncService ?? throw new ArgumentNullException(nameof(projectionRenderSyncService));
        _methodRegistry = methodRegistry ?? new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new AxisymmetricSourceProjectionMethod(),
            new SelfCalibratingAxisymmetricProjectionMethod(),
            new LeastSquaresAxisymmetricAlignmentProjectionMethod(),
        });
        _applicationLogService = applicationLogService;
        _projectionResultToCollisionSourceService = projectionResultToCollisionSourceService ?? new ProjectionResultToCollisionSourceService();

        ProjectionMethods = new ObservableCollection<ProjectionMethodOptionViewModel>(
            _methodRegistry.Methods.Select(method => new ProjectionMethodOptionViewModel { Method = method }));

        _selectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == ProjectionWorkspaceState.DefaultMethodId)
            ?? ProjectionMethods.FirstOrDefault();

        RunProjectionCommand = new AsyncRelayCommand(RunProjectionAsync, CanRunProjection);
        ApplyGeometryChangesCommand = new RelayCommand(ApplyGeometryChanges, CanApplyGeometryChanges);
        ImportHitPointsCsvCommand = new RelayCommand(ImportHitPointsCsv, () => !IsProjectionRunning);
        DeleteSelectedResultCommand = new RelayCommand(DeleteSelectedResult, () => !IsProjectionRunning && SelectedResult is not null);
        DeleteSelectedSceneCommand = new RelayCommand(DeleteSelectedScene, () => !IsProjectionRunning && CanDeleteSelectedScene);
        AddHybridSegmentCommand = new RelayCommand(AddHybridSegment, () => !IsProjectionRunning);
        RemoveSelectedHybridSegmentCommand = new RelayCommand(RemoveSelectedHybridSegment, () => !IsProjectionRunning && SelectedHybridSegment is not null);
        AddProjectedLightSourceToCollisionSceneCommand = new AsyncRelayCommand(AddProjectedLightSourceToCollisionAsync, CanAddProjectedLightSourceToCollision);

        _sceneCollectionService.Scenes.CollectionChanged += OnScenesCollectionChanged;
        AddHybridSegment();
        _appliedGeometry = CaptureDraftGeometry();
        RefreshAvailableScenes();
        RefreshTargetCollisionScenes();
    }

    public ObservableCollection<ProjectionMethodOptionViewModel> ProjectionMethods { get; }

    public ObservableCollection<CollisionSceneViewModel> AvailableScenes { get; } = new();
    public ObservableCollection<CollisionSceneViewModel> TargetCollisionScenes { get; } = new();

    public ICommand RunProjectionCommand { get; }
    public ICommand ApplyGeometryChangesCommand { get; }
    public ICommand ImportHitPointsCsvCommand { get; }
    public ICommand DeleteSelectedResultCommand { get; }
    public ICommand DeleteSelectedSceneCommand { get; }
    public ICommand AddHybridSegmentCommand { get; }
    public ICommand RemoveSelectedHybridSegmentCommand { get; }
    public ICommand AddProjectedLightSourceToCollisionSceneCommand { get; }

    public double PointSourceX { get; set; }
    public double PointSourceY { get; set; }
    public double PointSourceZ { get; set; }

    public double BeamOriginX { get => _beamOriginX; set => SetGeometryProperty(ref _beamOriginX, value); }
    public double BeamOriginY { get => _beamOriginY; set => SetGeometryProperty(ref _beamOriginY, value); }
    public double BeamOriginZ { get => _beamOriginZ; set => SetGeometryProperty(ref _beamOriginZ, value); }

    public double SourceFrameXx { get => _sourceFrameXx; set => SetGeometryProperty(ref _sourceFrameXx, value); }
    public double SourceFrameXy { get => _sourceFrameXy; set => SetGeometryProperty(ref _sourceFrameXy, value); }
    public double SourceFrameXz { get => _sourceFrameXz; set => SetGeometryProperty(ref _sourceFrameXz, value); }

    public double SourceFrameYx { get => _sourceFrameYx; set => SetGeometryProperty(ref _sourceFrameYx, value); }
    public double SourceFrameYy { get => _sourceFrameYy; set => SetGeometryProperty(ref _sourceFrameYy, value); }
    public double SourceFrameYz { get => _sourceFrameYz; set => SetGeometryProperty(ref _sourceFrameYz, value); }

    public double TiltPointX { get => _tiltPointX; set => SetPreviewProperty(ref _tiltPointX, value); }
    public double TiltPointY { get => _tiltPointY; set => SetPreviewProperty(ref _tiltPointY, value); }
    public double TiltPointZ { get => _tiltPointZ; set => SetPreviewProperty(ref _tiltPointZ, value); }

    public int LeastSquaresMaxIterations { get => _leastSquaresMaxIterations; set => SetProperty(ref _leastSquaresMaxIterations, value); }
    public double LeastSquaresConvergenceTolerance { get => _leastSquaresConvergenceTolerance; set => SetProperty(ref _leastSquaresConvergenceTolerance, value); }
    public double LeastSquaresPointStepScale { get => _leastSquaresPointStepScale; set => SetProperty(ref _leastSquaresPointStepScale, value); }
    public double LeastSquaresThetaStepScale { get => _leastSquaresThetaStepScale; set => SetProperty(ref _leastSquaresThetaStepScale, value); }
    public double LeastSquaresLambdaStepScale { get => _leastSquaresLambdaStepScale; set => SetProperty(ref _leastSquaresLambdaStepScale, value); }

    public AxisymmetricSourceKind[] AxisymmetricSourceKinds { get; } = Enum.GetValues<AxisymmetricSourceKind>();
    public AxisymmetricSourceKind SelectedProjectionGeometryKind
    {
        get => SelectedAxisymmetricSourceKind;
        set => SelectedAxisymmetricSourceKind = value;
    }

    public AxisymmetricSourceKind SelectedAxisymmetricSourceKind
    {
        get => _selectedAxisymmetricSourceKind;
        set
        {
            if (SetProperty(ref _selectedAxisymmetricSourceKind, value))
            {
                RaisePropertyChanged(nameof(IsProjectionGeometryCylinder));
                RaisePropertyChanged(nameof(IsProjectionGeometryConicalFrustum));
                RaisePropertyChanged(nameof(IsProjectionGeometryCircularOgive));
                RaisePropertyChanged(nameof(IsProjectionGeometryHybrid));
                GeometryDraftChanged();
            }
        }
    }

    public bool IsProjectionGeometryCylinder => SelectedAxisymmetricSourceKind == AxisymmetricSourceKind.Cylinder;
    public bool IsProjectionGeometryConicalFrustum => SelectedAxisymmetricSourceKind == AxisymmetricSourceKind.ConicalFrustum;
    public bool IsProjectionGeometryCircularOgive => SelectedAxisymmetricSourceKind == AxisymmetricSourceKind.CircularOgive;
    public bool IsProjectionGeometryHybrid => SelectedAxisymmetricSourceKind == AxisymmetricSourceKind.Hybrid;
    public bool HasUnappliedGeometryChanges => _appliedGeometry is not null && !GeometryEquals(CaptureDraftGeometry(), _appliedGeometry);

    public double GeometryRadiusStart { get => _geometryRadiusStart; set => SetGeometryProperty(ref _geometryRadiusStart, value); }
    public double GeometryRadiusEnd { get => _geometryRadiusEnd; set => SetGeometryProperty(ref _geometryRadiusEnd, value); }
    public double GeometryLength { get => _geometryLength; set => SetGeometryProperty(ref _geometryLength, value); }
    public double GeometryArcRadius { get => _geometryArcRadius; set => SetGeometryProperty(ref _geometryArcRadius, value); }
    public OgiveCurvatureDirection GeometryOgiveCurvatureDirection { get => _geometryOgiveCurvatureDirection; set => SetGeometryProperty(ref _geometryOgiveCurvatureDirection, value); }

    public ObservableCollection<HybridSourceSegmentItemViewModel> HybridSegments { get; } = new();
    public HybridSourceSegmentItemViewModel? SelectedHybridSegment
    {
        get => _selectedHybridSegment;
        set
        {
            if (SetProperty(ref _selectedHybridSegment, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }
    public HybridAxisymmetricSourceSegmentKind[] HybridSegmentKinds { get; } = Enum.GetValues<HybridAxisymmetricSourceSegmentKind>();
    public OgiveCurvatureDirection[] OgiveCurvatureDirections { get; } = Enum.GetValues<OgiveCurvatureDirection>();
    private double _tiltPointX;
    private double _tiltPointY;
    private double _tiltPointZ;

    public bool IsPointSourceMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.PointSource, StringComparison.OrdinalIgnoreCase);
    public bool IsAxisymmetricSourceMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.AxisymmetricSource, StringComparison.OrdinalIgnoreCase);
    public bool IsSelfCalibratingAxisymmetricMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.SelfCalibratingAxisymmetricSource, StringComparison.OrdinalIgnoreCase);
    public bool IsLeastSquaresAxisymmetricAlignmentMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource, StringComparison.OrdinalIgnoreCase);
    public bool IsAnyAxisymmetricMethodSelected => IsAxisymmetricSourceMethodSelected || IsSelfCalibratingAxisymmetricMethodSelected || IsLeastSquaresAxisymmetricAlignmentMethodSelected;
    public bool IsTiltPointMethodSelected => IsSelfCalibratingAxisymmetricMethodSelected || IsLeastSquaresAxisymmetricAlignmentMethodSelected;

    public bool IsProjectionRunning
    {
        get => _isProjectionRunning;
        private set
        {
            if (SetProperty(ref _isProjectionRunning, value))
            {
                RaisePropertyChanged(nameof(IsProgressVisible));
                RaisePropertyChanged(nameof(IsProjectionBusy));
                RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsProjectionBusy => IsProjectionRunning || _isBridgeBusy;
    public bool IsProgressVisible => IsProjectionBusy;

    public double ProjectionProgressPercent
    {
        get => _projectionProgressPercent;
        private set => SetProperty(ref _projectionProgressPercent, value);
    }

    public string ProjectionProgressMessage
    {
        get => _projectionProgressMessage;
        private set => SetProperty(ref _projectionProgressMessage, value);
    }

    public bool CanDeleteSelectedScene => SelectedScene is not null && _sceneCollectionService.Scenes.Contains(SelectedScene);

    public string NewResultName
    {
        get => _newResultName;
        set => SetProperty(ref _newResultName, value);
    }

    public IReadOnlyList<NamedProjectionResultState> SavedResults
    {
        get => SelectedScene?.ProjectionState.SavedResults ?? _emptyResults;
    }

    private static readonly IReadOnlyList<NamedProjectionResultState> _emptyResults = Array.Empty<NamedProjectionResultState>();

    public NamedProjectionResultState? SelectedResult
    {
        get
        {
            var scene = SelectedScene;
            if (scene?.ProjectionState.SelectedResultKey is null)
            {
                return null;
            }

            return scene.ProjectionState.SavedResults.FirstOrDefault(result => result.Key == scene.ProjectionState.SelectedResultKey);
        }
        set
        {
            if (SelectedScene is null)
            {
                return;
            }

            SelectedScene.ProjectionState.SelectedResultKey = value?.Key;
            RefreshViewport();
            RaisePropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public ProjectionMethodOptionViewModel? SelectedMethod
    {
        get => _selectedMethod;
        set
        {
            if (!SetProperty(ref _selectedMethod, value))
            {
                return;
            }

            if (SelectedScene is not null)
            {
                SelectedScene.ProjectionState.SelectedMethodId = value?.Id ?? ProjectionWorkspaceState.DefaultMethodId;
            }

            RaisePropertyChanged(nameof(IsPointSourceMethodSelected));
            RaisePropertyChanged(nameof(IsAxisymmetricSourceMethodSelected));
            RaisePropertyChanged(nameof(IsSelfCalibratingAxisymmetricMethodSelected));
            RaisePropertyChanged(nameof(IsLeastSquaresAxisymmetricAlignmentMethodSelected));
            RaisePropertyChanged(nameof(IsAnyAxisymmetricMethodSelected));
            RaisePropertyChanged(nameof(IsTiltPointMethodSelected));
            RaiseCanExecuteChanged();
        }
    }

    public bool ShowPanels
    {
        get => _showPanels;
        set
        {
            if (SetProperty(ref _showPanels, value)) RefreshViewport(zoomExtents: false);
        }
    }

    public bool ShowMeasuredCorners
    {
        get => _showMeasuredCorners;
        set
        {
            if (SetProperty(ref _showMeasuredCorners, value)) RefreshViewport(zoomExtents: false);
        }
    }

    public bool IncludeNaturalPoints
    {
        get => _includeNaturalPoints;
        set
        {
            if (!SetProperty(ref _includeNaturalPoints, value)) return;
            RefreshViewport(zoomExtents: false);
            RaiseCanExecuteChanged();
        }
    }

    public CollisionSceneViewModel? SelectedScene
    {
        get => _selectedScene;
        set
        {
            if (ReferenceEquals(_selectedScene, value))
            {
                return;
            }

            var previousScene = _selectedScene;
            if (!SetProperty(ref _selectedScene, value))
            {
                return;
            }

            if (previousScene is not null)
            {
                previousScene.ProjectionState.SavedResults.CollectionChanged -= OnSavedResultsCollectionChanged;
            }

            if (value is not null)
            {
                SelectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == value.ProjectionState.SelectedMethodId)
                    ?? ProjectionMethods.FirstOrDefault();
                value.ProjectionState.SavedResults.CollectionChanged += OnSavedResultsCollectionChanged;
            }

            RaisePropertyChanged(nameof(SavedResults));
            RaisePropertyChanged(nameof(SelectedResult));
            RaisePropertyChanged(nameof(CanDeleteSelectedScene));
            if (!_isApplyingWorkspaceState) RefreshViewport();
            RaiseCanExecuteChanged();
        }
    }


    public CollisionSceneViewModel? SelectedTargetCollisionScene
    {
        get => _selectedTargetCollisionScene;
        set
        {
            if (SetProperty(ref _selectedTargetCollisionScene, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ProjectionWorkspaceStateDto ExportWorkspaceState()
    {
        return new ProjectionWorkspaceStateDto
        {
            SelectedSceneName = SelectedScene?.Name,
            ShowPanels = ShowPanels,
            ShowMeasuredCorners = ShowMeasuredCorners,
            IncludeNaturalPoints = IncludeNaturalPoints,
            SelectedMethodId = SelectedMethod?.Id ?? ProjectionWorkspaceState.DefaultMethodId,
            ProjectionGeometryKind = _appliedGeometry.Kind,
            BeamOriginX = _appliedGeometry.BeamOriginX, BeamOriginY = _appliedGeometry.BeamOriginY, BeamOriginZ = _appliedGeometry.BeamOriginZ,
            SourceFrameXx = _appliedGeometry.SourceFrameXx, SourceFrameXy = _appliedGeometry.SourceFrameXy, SourceFrameXz = _appliedGeometry.SourceFrameXz,
            SourceFrameYx = _appliedGeometry.SourceFrameYx, SourceFrameYy = _appliedGeometry.SourceFrameYy, SourceFrameYz = _appliedGeometry.SourceFrameYz,
            GeometryRadiusStart = _appliedGeometry.RadiusStart,
            GeometryRadiusEnd = _appliedGeometry.RadiusEnd,
            GeometryLength = _appliedGeometry.Length,
            GeometryArcRadius = _appliedGeometry.ArcRadius,
            GeometryOgiveCurvatureDirection = _appliedGeometry.Curvature,
            HybridSegments = _appliedGeometry.HybridSegments.Select(segment => new AxisymmetricSourceSegmentStateDto
            {
                SegmentKind = segment.Kind,
                Length = segment.Length,
                RadiusStart = segment.RadiusStart,
                RadiusEnd = segment.RadiusEnd,
                ArcRadius = segment.Kind == HybridAxisymmetricSourceSegmentKind.CircularOgive ? segment.ArcRadius : null,
                OgiveCurvatureDirection = segment.Curvature,
            }).ToList(),
            TiltPointX = TiltPointX,
            TiltPointY = TiltPointY,
            TiltPointZ = TiltPointZ,
            LeastSquaresMaxIterations = LeastSquaresMaxIterations,
            LeastSquaresConvergenceTolerance = LeastSquaresConvergenceTolerance,
            LeastSquaresPointStepScale = LeastSquaresPointStepScale,
            LeastSquaresThetaStepScale = LeastSquaresThetaStepScale,
            LeastSquaresLambdaStepScale = LeastSquaresLambdaStepScale,
            HybridTiltPointX = (float)TiltPointX,
            HybridTiltPointY = (float)TiltPointY,
            HybridTiltPointZ = (float)TiltPointZ,
        };
    }

    public void ApplyWorkspaceState(ProjectionWorkspaceStateDto state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _isApplyingWorkspaceState = true;
        try
        {
            ShowPanels = state.ShowPanels ?? true;
            ShowMeasuredCorners = state.ShowMeasuredCorners ?? true;
            IncludeNaturalPoints = state.IncludeNaturalPoints ?? true;
            SelectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == state.SelectedMethodId)
                ?? ProjectionMethods.FirstOrDefault(method => method.Id == ProjectionWorkspaceState.DefaultMethodId)
                ?? ProjectionMethods.FirstOrDefault();

            SelectedAxisymmetricSourceKind = state.ProjectionGeometryKind;
            BeamOriginX = state.BeamOriginX; BeamOriginY = state.BeamOriginY; BeamOriginZ = state.BeamOriginZ;
            SourceFrameXx = state.SourceFrameXx; SourceFrameXy = state.SourceFrameXy; SourceFrameXz = state.SourceFrameXz;
            SourceFrameYx = state.SourceFrameYx; SourceFrameYy = state.SourceFrameYy; SourceFrameYz = state.SourceFrameYz;
            GeometryRadiusStart = state.GeometryRadiusStart;
            GeometryRadiusEnd = state.GeometryRadiusEnd;
            GeometryLength = state.GeometryLength;
            GeometryArcRadius = state.GeometryArcRadius;
            GeometryOgiveCurvatureDirection = state.GeometryOgiveCurvatureDirection;

            foreach (var segment in HybridSegments)
            {
                DetachHybridSegment(segment);
            }
            HybridSegments.Clear();
            if (state.HybridSegments.Count > 0)
            {
                foreach (var segment in state.HybridSegments)
                {
                    var newSegment = new HybridSourceSegmentItemViewModel
                    {
                        SegmentKind = segment.SegmentKind,
                        Length = segment.Length,
                        RadiusStart = segment.RadiusStart,
                        RadiusEnd = segment.RadiusEnd,
                        ArcRadius = segment.ArcRadius ?? 20f,
                        OgiveCurvatureDirection = segment.OgiveCurvatureDirection,
                    };
                    AttachHybridSegment(newSegment);
                    HybridSegments.Add(newSegment);
                }

                SynchronizeHybridSegmentContinuity();
            }
            EnsureDefaultHybridSegment();

            TiltPointX = state.TiltPointX;
            TiltPointY = state.TiltPointY;
            TiltPointZ = state.TiltPointZ;
            LeastSquaresMaxIterations = state.LeastSquaresMaxIterations ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default.MaxIterations;
            LeastSquaresConvergenceTolerance = state.LeastSquaresConvergenceTolerance ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default.ConvergenceTolerance;
            LeastSquaresPointStepScale = state.LeastSquaresPointStepScale ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default.PointStepScale;
            LeastSquaresThetaStepScale = state.LeastSquaresThetaStepScale ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default.ThetaStepScale;
            LeastSquaresLambdaStepScale = state.LeastSquaresLambdaStepScale ?? LeastSquaresAxisymmetricAlignmentSolverSettings.Default.LambdaStepScale;

            if (TiltPointX == 0d && TiltPointY == 0d && TiltPointZ == 0d &&
                (state.HybridTiltPointX != 0f || state.HybridTiltPointY != 0f || state.HybridTiltPointZ != 0f))
            {
                TiltPointX = state.HybridTiltPointX;
                TiltPointY = state.HybridTiltPointY;
                TiltPointZ = state.HybridTiltPointZ;
            }

            SelectedScene = AvailableScenes.FirstOrDefault(scene => scene.Name == state.SelectedSceneName)
                ?? AvailableScenes.FirstOrDefault();
            _appliedGeometry = CaptureDraftGeometry();
        }
        finally
        {
            _isApplyingWorkspaceState = false;
        }

        RaisePropertyChanged(nameof(IsProjectionGeometryCylinder));
        RaisePropertyChanged(nameof(IsProjectionGeometryConicalFrustum));
        RaisePropertyChanged(nameof(IsProjectionGeometryCircularOgive));
        RaisePropertyChanged(nameof(IsProjectionGeometryHybrid));
        RaisePropertyChanged(nameof(SavedResults));
        RaisePropertyChanged(nameof(SelectedResult));
        RaiseCanExecuteChanged();
        RefreshViewport();
    }

    private bool CanRunProjection() =>
        !IsProjectionRunning && !HasUnappliedGeometryChanges && SelectedScene is not null && GetEffectiveProjectionPoints(SelectedScene).Count > 0 && SelectedMethod is not null;

    private void ImportHitPointsCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog() != true)
        {
            SetStatus("CSV import canceled.");
            return;
        }

        ProjectionHitPointCsvImportResult importResult;
        try
        {
            importResult = _projectionHitPointCsvImportService.Import(dialog.FileName);
        }
        catch (ArgumentException ex)
        {
            SetStatus(ex.Message, ApplicationLogLevel.Warning, ex);
            return;
        }

        if (importResult.HolePoints.Count == 0)
        {
            SetStatus("No valid hit-point rows were found in the selected CSV.", ApplicationLogLevel.Warning);
            return;
        }

        var baseName = string.IsNullOrWhiteSpace(importResult.SceneName) ? "Imported Hit Points" : importResult.SceneName;
        var sceneName = ResolveImportedSceneName(baseName);
        var scene = new CollisionSceneViewModel(sceneName)
        {
            IsProjectionOnly = true,
        };

        foreach (var point in importResult.HolePoints)
        {
            scene.HolePoints.Add(point);
        }

        _sceneCollectionService.AddScene(scene, selectScene: false);
        EnsureDefaultHybridSegment();
        RefreshAvailableScenes();
        RefreshTargetCollisionScenes();
        SelectedScene = scene;

        SetStatus($"Imported {importResult.HolePoints.Count} hole points into projection scene '{sceneName}'. Skipped {importResult.SkippedRowCount} invalid rows.", ApplicationLogLevel.Success);
    }

    private async Task RunProjectionAsync()
    {
        var scene = SelectedScene;
        if (scene is null)
        {
            SetStatus("Select a scene with holes before running projection.", ApplicationLogLevel.Warning);
            return;
        }

        if (SelectedMethod is null)
        {
            SetStatus("Select a projection methodology.", ApplicationLogLevel.Warning);
            return;
        }

        if (IsLeastSquaresAxisymmetricAlignmentMethodSelected)
        {
            if (LeastSquaresMaxIterations <= 0)
            {
                SetStatus("Least-squares max iterations must be greater than zero.", ApplicationLogLevel.Warning);
                return;
            }

            if (LeastSquaresConvergenceTolerance <= 0d)
            {
                SetStatus("Least-squares error bound must be greater than zero.", ApplicationLogLevel.Warning);
                return;
            }

            if (LeastSquaresPointStepScale <= 0d || LeastSquaresThetaStepScale <= 0d || LeastSquaresLambdaStepScale <= 0d)
            {
                SetStatus("Least-squares step scales must be positive.", ApplicationLogLevel.Warning);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(NewResultName))
        {
            SetStatus("Provide a projection result name.", ApplicationLogLevel.Warning);
            return;
        }

        var methodOption = SelectedMethod;
        var method = methodOption.Method;
        var resultName = NewResultName;
        var projectionPoints = GetEffectiveProjectionPoints(scene);
        if (projectionPoints.Count == 0)
        {
            SetStatus("The selected scene has no effective projection input points.", ApplicationLogLevel.Warning);
            return;
        }
        var request = new ProjectionRequest
        {
            HolePoints = projectionPoints,
            Parameters = BuildParameters(method),
            Progress = new Progress<ProjectionProgress>(report =>
            {
                if (report.Percent is not null)
                {
                    ProjectionProgressPercent = Math.Clamp(report.Percent.Value, 0d, 100d);
                    var bucket = (int)(ProjectionProgressPercent / 10d);
                    if (bucket > _lastLoggedProgressBucket)
                    {
                        _lastLoggedProgressBucket = bucket;
                        _applicationLogService?.LogInfo($"Projection progress {ProjectionProgressPercent:F0}% - {report.Message}", nameof(ProjectionWorkspaceViewModel));
                    }
                }
                ProjectionProgressMessage = report.Message;
            }),
        };

        IsProjectionRunning = true;
        ProjectionProgressPercent = 0;
        ProjectionProgressMessage = "Preparing projection...";
        _lastLoggedProgressBucket = -1;
        _applicationLogService?.LogInfo($"Projection scene: {scene.Name}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Projection method: {methodOption.DisplayName} ({methodOption.Id})", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Hole points: {scene.HolePoints.Count}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Natural points available: {scene.NaturalPoints.Count}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Include natural points: {(IncludeNaturalPoints ? "Yes" : "No")}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Total projection input points: {projectionPoints.Count}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo("Projection run started.", nameof(ProjectionWorkspaceViewModel));

        try
        {
            var result = await Task.Run(() => method.Execute(request));
            var namedResult = SceneProjectionStateUpdater.SaveResult(scene.ProjectionState, resultName, result);
            NewResultName = $"Projection Result {scene.ProjectionState.SavedResults.Count + 1}";
            scene.ProjectionState.SelectedMethodId = methodOption.Id;
            SelectedResult = namedResult;

            SetStatus(result.AxisymmetricSource is null
                ? $"Projection completed and saved as '{namedResult.DisplayName}' ({result.Rays.Count} ray(s))."
                : $"Axisymmetric projection completed and saved as '{namedResult.DisplayName}' ({result.AxisymmetricSource.Points.Count} reconstructed source points).",
                ApplicationLogLevel.Success);
            LogProjectionSummary(result);
            _applicationLogService?.LogSuccess("Projection run completed.", nameof(ProjectionWorkspaceViewModel));
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, ApplicationLogLevel.Error, ex);
            _applicationLogService?.LogError("Projection run failed.", ex, nameof(ProjectionWorkspaceViewModel));
        }
        finally
        {
            IsProjectionRunning = false;
            ProjectionProgressMessage = string.Empty;
        }
    }

    private void DeleteSelectedResult()
    {
        var scene = SelectedScene;
        var selectedResult = SelectedResult;
        if (scene is null || selectedResult is null)
        {
            SetStatus("Select a projection result to delete.", ApplicationLogLevel.Warning);
            return;
        }

        var deleted = SceneProjectionStateUpdater.DeleteResult(scene.ProjectionState, selectedResult);
        if (!deleted)
        {
            SetStatus("Selected projection result could not be deleted.", ApplicationLogLevel.Warning);
            return;
        }

        RaisePropertyChanged(nameof(SavedResults));
        RaisePropertyChanged(nameof(SelectedResult));
        RefreshViewport();
        RaiseCanExecuteChanged();
        SetStatus($"Deleted projection result '{selectedResult.DisplayName}'.", ApplicationLogLevel.Success);
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
        EnsureDefaultHybridSegment();
        RefreshAvailableScenes();
        RefreshTargetCollisionScenes();
        RefreshViewport();
        RaiseCanExecuteChanged();
        SetStatus($"Deleted scene '{deletedName}'.", ApplicationLogLevel.Success);
    }

    private void SetStatus(string message, ApplicationLogLevel level = ApplicationLogLevel.Info, Exception? exception = null)
    {
        StatusMessage = message;
        if (_applicationLogService is null)
        {
            return;
        }

        switch (level)
        {
            case ApplicationLogLevel.Trace:
                _applicationLogService.LogTrace(message, nameof(ProjectionWorkspaceViewModel));
                break;
            case ApplicationLogLevel.Info:
                _applicationLogService.LogInfo(message, nameof(ProjectionWorkspaceViewModel));
                break;
            case ApplicationLogLevel.Success:
                _applicationLogService.LogSuccess(message, nameof(ProjectionWorkspaceViewModel));
                break;
            case ApplicationLogLevel.Warning:
                _applicationLogService.LogWarning(message, nameof(ProjectionWorkspaceViewModel));
                break;
            case ApplicationLogLevel.Error:
                _applicationLogService.LogError(message, exception, nameof(ProjectionWorkspaceViewModel));
                break;
        }
    }

    private IProjectionParameters BuildParameters(IProjectionMethod method)
    {
        if (method.Metadata.Id == ProjectionMethodIds.PointSource)
        {
            return new PointSourceProjectionParameters(
                new Point3(PointSourceX, PointSourceY, PointSourceZ),
                new Point3(_appliedGeometry.BeamOriginX, _appliedGeometry.BeamOriginY, _appliedGeometry.BeamOriginZ),
                new Vector3D(_appliedGeometry.SourceFrameXx, _appliedGeometry.SourceFrameXy, _appliedGeometry.SourceFrameXz),
                new Vector3D(_appliedGeometry.SourceFrameYx, _appliedGeometry.SourceFrameYy, _appliedGeometry.SourceFrameYz));
        }

        var profileDefinition = BuildAxisymmetricSourceProfileDefinition(method);

        if (method.Metadata.Id == ProjectionMethodIds.AxisymmetricSource)
        {
            return new AxisymmetricSourceProjectionParameters(
                new Point3(_appliedGeometry.BeamOriginX, _appliedGeometry.BeamOriginY, _appliedGeometry.BeamOriginZ),
                new Vector3D(_appliedGeometry.SourceFrameXx, _appliedGeometry.SourceFrameXy, _appliedGeometry.SourceFrameXz),
                new Vector3D(_appliedGeometry.SourceFrameYx, _appliedGeometry.SourceFrameYy, _appliedGeometry.SourceFrameYz),
                profileDefinition);
        }

        if (method.Metadata.Id == ProjectionMethodIds.SelfCalibratingAxisymmetricSource)
        {
            return new SelfCalibratingAxisymmetricProjectionParameters(
                new Point3(_appliedGeometry.BeamOriginX, _appliedGeometry.BeamOriginY, _appliedGeometry.BeamOriginZ),
                new Vector3D(_appliedGeometry.SourceFrameXx, _appliedGeometry.SourceFrameXy, _appliedGeometry.SourceFrameXz),
                new Vector3D(_appliedGeometry.SourceFrameYx, _appliedGeometry.SourceFrameYy, _appliedGeometry.SourceFrameYz),
                profileDefinition,
                new Point3(TiltPointX, TiltPointY, TiltPointZ));
        }

        if (method.Metadata.Id == ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource)
        {
            var defaultSettings = LeastSquaresAxisymmetricAlignmentSolverSettings.Default;
            var leastSquaresSettings = new LeastSquaresAxisymmetricAlignmentSolverSettings
            {
                MaxIterations = LeastSquaresMaxIterations,
                ConvergenceTolerance = LeastSquaresConvergenceTolerance,
                PointStepScale = LeastSquaresPointStepScale,
                ThetaStepScale = LeastSquaresThetaStepScale,
                LambdaStepScale = LeastSquaresLambdaStepScale,
                FiniteDifferenceRelativeStep = defaultSettings.FiniteDifferenceRelativeStep,
                BacktrackingFactor = defaultSettings.BacktrackingFactor,
                MaxBacktrackingAttempts = defaultSettings.MaxBacktrackingAttempts,
                ProgressAngularThresholdDegrees = defaultSettings.ProgressAngularThresholdDegrees,
            };

            return new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                new Point3(_appliedGeometry.BeamOriginX, _appliedGeometry.BeamOriginY, _appliedGeometry.BeamOriginZ),
                new Vector3D(_appliedGeometry.SourceFrameXx, _appliedGeometry.SourceFrameXy, _appliedGeometry.SourceFrameXz),
                new Vector3D(_appliedGeometry.SourceFrameYx, _appliedGeometry.SourceFrameYy, _appliedGeometry.SourceFrameYz),
                profileDefinition,
                new Point3(TiltPointX, TiltPointY, TiltPointZ),
                leastSquaresSettings);
        }

        throw new InvalidOperationException($"Projection method '{method.Metadata.Id}' is not yet supported by the workspace UI parameter panel.");
    }

    private IAxisymmetricSourceProfile RequireProjectionGeometryProfile()
    {
        return BuildSelectedProjectionGeometryProfile()
            ?? throw new InvalidOperationException("Projection geometry profile is required for axisymmetric projection methods.");
    }

    private void LogProjectionSummary(ProjectionComputationResult result)
    {
        if (_applicationLogService is null)
        {
            return;
        }

        if (result.MethodId == ProjectionMethodIds.PointSource)
        {
            _applicationLogService.LogInfo($"Generated rays: {result.Rays.Count}", nameof(ProjectionWorkspaceViewModel));
            if (result.PointSourceOrigin is Point3 pointSourceOrigin)
            {
                _applicationLogService.LogInfo(
                    $"Point source origin: ({pointSourceOrigin.X:F4}, {pointSourceOrigin.Y:F4}, {pointSourceOrigin.Z:F4})",
                    nameof(ProjectionWorkspaceViewModel));
            }

            return;
        }

        var axisymmetric = result.AxisymmetricSource;
        if (axisymmetric is null)
        {
            return;
        }

        _applicationLogService.LogInfo("Profile kind: runtime axisymmetric state", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService.LogInfo($"Profile length: {axisymmetric.Length:F6}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService.LogInfo($"Reconstructed source points: {axisymmetric.Points.Count}", nameof(ProjectionWorkspaceViewModel));

        var fitEntries = axisymmetric.Points
            .Select((point, index) => new { point.FitError, Index = index })
            .Where(item => item.FitError.HasValue)
            .Select(item => new { FitError = item.FitError!.Value, item.Index })
            .ToList();

        if (fitEntries.Count > 0)
        {
            var meanFitError = fitEntries.Average(item => item.FitError);
            var rmsFitError = Math.Sqrt(fitEntries.Average(item => item.FitError * item.FitError));
            var minFitErrorEntry = fitEntries.MinBy(item => item.FitError)!;
            var maxFitErrorEntry = fitEntries.MaxBy(item => item.FitError)!;

            _applicationLogService.LogInfo($"Mean fit error: {meanFitError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"RMS fit error: {rmsFitError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Min fit error: {minFitErrorEntry.FitError:F6} at hole index {minFitErrorEntry.Index}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogWarning($"Max fit error: {maxFitErrorEntry.FitError:F6} at hole index {maxFitErrorEntry.Index}", nameof(ProjectionWorkspaceViewModel));
        }

        if (result.MethodId == ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource && axisymmetric.LeastSquaresDiagnostics is not null)
        {
            var diagnostics = axisymmetric.LeastSquaresDiagnostics;
            _applicationLogService.LogSuccess("Least-squares axisymmetric alignment completed.", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Initial lambda: {diagnostics.InitialLambda:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Refined lambda: {diagnostics.RefinedLambda:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Initial mean alignment error: {diagnostics.InitialMeanAlignmentError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Final mean alignment error: {diagnostics.FinalMeanAlignmentError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Final RMS alignment error: {diagnostics.FinalRmsAlignmentError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Final mean angular error: {diagnostics.FinalMeanAngularErrorDegrees:F4} deg", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogWarning($"Max angular error: {diagnostics.FinalMaxAngularErrorDegrees:F4} deg at hole index {diagnostics.MaxAngularErrorHoleIndex?.ToString() ?? "n/a"}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Iterations: {diagnostics.Iterations}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Converged: {diagnostics.Converged}", nameof(ProjectionWorkspaceViewModel));
            return;
        }

        if (result.MethodId != ProjectionMethodIds.SelfCalibratingAxisymmetricSource)
        {
            return;
        }

        _applicationLogService.LogSuccess("Self-calibrating axisymmetric projection completed.", nameof(ProjectionWorkspaceViewModel));
        if (axisymmetric.EstimatedTiltWeight.HasValue)
        {
            _applicationLogService.LogSuccess($"Estimated lambda: {axisymmetric.EstimatedTiltWeight.Value:F6}", nameof(ProjectionWorkspaceViewModel));
        }

        if (axisymmetric.Diagnostics is null)
        {
            return;
        }

        _applicationLogService.LogInfo($"Regularity weight: {axisymmetric.Diagnostics.RegularityWeight:F6}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService.LogInfo($"Candidates evaluated: {axisymmetric.Diagnostics.CandidateScores.Count}", nameof(ProjectionWorkspaceViewModel));

        var bestCandidate = axisymmetric.Diagnostics.CandidateScores.MinBy(candidate => candidate.Score);
        if (bestCandidate is not null)
        {
            _applicationLogService.LogInfo($"Best candidate score: {bestCandidate.Score:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Best candidate regularity score: {bestCandidate.RegularityError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Best candidate mean fit error: {bestCandidate.MeanFitError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Best candidate total score: {bestCandidate.Score:F6}", nameof(ProjectionWorkspaceViewModel));
        }
    }

    private void RefreshViewport(bool zoomExtents = true)
    {
        var scene = SelectedScene;
        var holePoints = scene?.HolePoints?.ToList() ?? new List<Point3>();
        var naturalPoints = IncludeNaturalPoints ? scene?.NaturalPoints?.ToList() ?? new List<Point3>() : new List<Point3>();
        IReadOnlyList<RectangularPrism> panels = ShowPanels && scene is not null
            ? scene.Prisms.Select(PrismGeometryConverter.CreateDomainPrism).ToList()
            : Array.Empty<RectangularPrism>();
        IReadOnlyList<Point3> measuredCorners = ShowMeasuredCorners && scene is not null
            ? scene.MeasuredCornerPoints.ToList()
            : Array.Empty<Point3>();
        var result = scene?.ProjectionState.SelectedResult;
        _projectionRenderSyncService.SyncProjectionScene(
            holePoints,
            naturalPoints,
            result,
            BuildSelectedProjectionGeometryProfile(),
            BuildPreviewFrame(),
            previewAsGhost: true,
            previewTiltPointLocal: new Point3(TiltPointX, TiltPointY, TiltPointZ),
            panels: panels,
            measuredCornerPoints: measuredCorners,
            zoomExtents: zoomExtents);
    }

    private void OnScenesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAvailableScenes();
        RefreshTargetCollisionScenes();
    }

    private void RefreshAvailableScenes()
    {
        var selected = SelectedScene;

        AvailableScenes.Clear();
        foreach (var scene in _sceneCollectionService.Scenes.Where(scene => scene.HolePoints.Count > 0 || scene.NaturalPoints.Count > 0))
        {
            AvailableScenes.Add(scene);
        }

        SelectedScene = selected is not null && AvailableScenes.Contains(selected)
            ? selected
            : AvailableScenes.FirstOrDefault();

        RaiseCanExecuteChanged();
    }

    private IReadOnlyList<Point3> GetEffectiveProjectionPoints(CollisionSceneViewModel scene)
        => ProjectionPointSelector.BuildEffectivePoints(scene.HolePoints, scene.NaturalPoints, IncludeNaturalPoints);

    private void RefreshTargetCollisionScenes()
    {
        var previous = SelectedTargetCollisionScene;

        TargetCollisionScenes.Clear();
        foreach (var scene in _sceneCollectionService.Scenes.Where(scene => !scene.IsProjectionOnly))
        {
            TargetCollisionScenes.Add(scene);
        }

        SelectedTargetCollisionScene = previous is not null && TargetCollisionScenes.Contains(previous)
            ? previous
            : TargetCollisionScenes.FirstOrDefault();
    }

    private string ResolveImportedSceneName(string baseName)
    {
        if (_sceneCollectionService.Scenes.All(scene => !string.Equals(scene.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        var suffix = 2;
        while (true)
        {
            var candidate = $"{baseName} (Imported {suffix})";
            if (_sceneCollectionService.Scenes.All(scene => !string.Equals(scene.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }

            suffix++;
        }
    }


    private void AddHybridSegment()
    {
        var previous = HybridSegments.LastOrDefault();
        var radius = previous?.RadiusEnd ?? (float)GeometryRadiusStart;
        var segment = new HybridSourceSegmentItemViewModel
        {
            SegmentIndex = HybridSegments.Count + 1,
            IsRadiusStartEditable = HybridSegments.Count == 0,
            RadiusStart = radius,
            RadiusEnd = radius,
        };
        AttachHybridSegment(segment);
        HybridSegments.Add(segment);

        SynchronizeHybridSegmentContinuity();
        SelectedHybridSegment = HybridSegments.LastOrDefault();
        GeometryDraftChanged();
    }

    private void RemoveSelectedHybridSegment()
    {
        if (SelectedHybridSegment is null)
        {
            SetStatus("Select a hybrid segment to remove.", ApplicationLogLevel.Warning);
            return;
        }

        DetachHybridSegment(SelectedHybridSegment);
        HybridSegments.Remove(SelectedHybridSegment);
        if (HybridSegments.Count == 0)
        {
            AddHybridSegment();
        }

        SynchronizeHybridSegmentContinuity();
        SelectedHybridSegment = HybridSegments.LastOrDefault();
        GeometryDraftChanged();
    }

    private void SynchronizeHybridSegmentContinuity()
    {
        for (var i = 0; i < HybridSegments.Count; i++)
        {
            HybridSegments[i].SegmentIndex = i + 1;
            HybridSegments[i].IsRadiusStartEditable = i == 0;
            if (i > 0)
            {
                HybridSegments[i].RadiusStart = HybridSegments[i - 1].RadiusEnd;
            }

            if (HybridSegments[i].SegmentKind == HybridAxisymmetricSourceSegmentKind.Cylinder && HybridSegments[i].RadiusEnd != HybridSegments[i].RadiusStart)
            {
                HybridSegments[i].RadiusEnd = HybridSegments[i].RadiusStart;
            }
        }
    }

    private void EnsureDefaultHybridSegment()
    {
        if (HybridSegments.Count == 0)
        {
            AddHybridSegment();
            return;
        }

        SynchronizeHybridSegmentContinuity();
        SelectedHybridSegment ??= HybridSegments.FirstOrDefault();
    }

    private void AttachHybridSegment(HybridSourceSegmentItemViewModel segment) => segment.PropertyChanged += OnHybridSegmentPropertyChanged;
    private void DetachHybridSegment(HybridSourceSegmentItemViewModel segment) => segment.PropertyChanged -= OnHybridSegmentPropertyChanged;
    private void OnHybridSegmentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HybridSourceSegmentItemViewModel.SegmentKind)
            or nameof(HybridSourceSegmentItemViewModel.Radius)
            or nameof(HybridSourceSegmentItemViewModel.RadiusStart)
            or nameof(HybridSourceSegmentItemViewModel.RadiusEnd)
            or nameof(HybridSourceSegmentItemViewModel.Length)
            or nameof(HybridSourceSegmentItemViewModel.ArcRadius)
            or nameof(HybridSourceSegmentItemViewModel.OgiveCurvatureDirection))
        {
            SynchronizeHybridSegmentContinuity();
            GeometryDraftChanged();
        }
    }

    private void GeometryDraftChanged()
    {
        if (_isApplyingWorkspaceState) return;
        RaisePropertyChanged(nameof(HasUnappliedGeometryChanges));
        RaiseCanExecuteChanged();
    }

    private bool CanApplyGeometryChanges() => !IsProjectionRunning && HasUnappliedGeometryChanges;

    private void ApplyGeometryChanges()
    {
        var candidate = CaptureDraftGeometry();
        try
        {
            PointSourceFrameBuilder.Build(candidate.Origin, candidate.FrameX, candidate.FrameY);
            BuildProfile(candidate);
        }
        catch (ArgumentException ex)
        {
            SetStatus($"Cannot apply geometry: {ex.Message}", ApplicationLogLevel.Warning);
            RaiseCanExecuteChanged();
            return;
        }

        _appliedGeometry = candidate;
        RaisePropertyChanged(nameof(HasUnappliedGeometryChanges));
        RaiseCanExecuteChanged();
        RefreshViewport();
        SetStatus("Projection geometry changes applied.", ApplicationLogLevel.Success);
    }

    private ProjectionGeometrySnapshot CaptureDraftGeometry() => new(
        SelectedAxisymmetricSourceKind, BeamOriginX, BeamOriginY, BeamOriginZ,
        SourceFrameXx, SourceFrameXy, SourceFrameXz, SourceFrameYx, SourceFrameYy, SourceFrameYz,
        GeometryRadiusStart, GeometryRadiusEnd, GeometryLength, GeometryArcRadius, GeometryOgiveCurvatureDirection,
        HybridSegments.Select(x => new HybridSegmentSnapshot(x.SegmentKind, x.Length, x.RadiusStart, x.RadiusEnd, x.ArcRadius, x.OgiveCurvatureDirection)).ToArray());

    private static bool GeometryEquals(ProjectionGeometrySnapshot left, ProjectionGeometrySnapshot right) =>
        left with { HybridSegments = Array.Empty<HybridSegmentSnapshot>() } == right with { HybridSegments = Array.Empty<HybridSegmentSnapshot>() }
        && left.HybridSegments.SequenceEqual(right.HybridSegments);

    private static IAxisymmetricSourceProfile BuildProfile(ProjectionGeometrySnapshot geometry) => geometry.Kind switch
    {
        AxisymmetricSourceKind.Cylinder => new AxisymmetricSourceProfile((float)geometry.RadiusStart, (float)geometry.Length),
        AxisymmetricSourceKind.ConicalFrustum => new ConicalFrustumSourceProfile((float)geometry.RadiusStart, (float)geometry.RadiusEnd, (float)geometry.Length),
        AxisymmetricSourceKind.CircularOgive => new CircularOgiveSourceProfile((float)geometry.RadiusStart, (float)geometry.RadiusEnd, (float)geometry.Length, (float)geometry.ArcRadius, geometry.Curvature),
        AxisymmetricSourceKind.Hybrid => new HybridAxisymmetricSourceProfile(geometry.HybridSegments.Select(x => new HybridAxisymmetricSourceSegmentDefinition(x.Kind, x.Length, x.RadiusStart, x.RadiusEnd, x.Kind == HybridAxisymmetricSourceSegmentKind.CircularOgive ? x.ArcRadius : null, x.Curvature)).ToList()),
        _ => throw new ArgumentException($"Unsupported projection geometry kind '{geometry.Kind}'."),
    };

    private void SetGeometryProperty<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value)) GeometryDraftChanged();
    }

    private void SetPreviewProperty<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value) && !_isApplyingWorkspaceState) RefreshViewport(zoomExtents: false);
    }


    public AxisymmetricSourceProfileDefinition BuildCurrentProfileDefinition()
    {
        return BuildAxisymmetricSourceProfileDefinition(SelectedMethod?.Method ?? new AxisymmetricSourceProjectionMethod());
    }

    private LaserCollisionIn3DObjects.Domain.Geometry.AxisymmetricSourceProfileDefinition BuildAxisymmetricSourceProfileDefinition(IProjectionMethod method)
    {
        if (method.Metadata.Id == ProjectionMethodIds.PointSource)
        {
            return new LaserCollisionIn3DObjects.Domain.Geometry.AxisymmetricSourceProfileDefinition();
        }

        return _appliedGeometry.Kind switch
        {
            AxisymmetricSourceKind.Cylinder => new LaserCollisionIn3DObjects.Domain.Geometry.AxisymmetricSourceProfileDefinition
            {
                Kind = AxisymmetricSourceKind.Cylinder,
                Radius = (float)_appliedGeometry.RadiusStart,
                Length = (float)_appliedGeometry.Length,
                Height = (float)_appliedGeometry.Length,
            },
            AxisymmetricSourceKind.ConicalFrustum => new LaserCollisionIn3DObjects.Domain.Geometry.AxisymmetricSourceProfileDefinition
            {
                Kind = AxisymmetricSourceKind.ConicalFrustum,
                RadiusStart = (float)_appliedGeometry.RadiusStart,
                RadiusEnd = (float)_appliedGeometry.RadiusEnd,
                Length = (float)_appliedGeometry.Length,
            },
            AxisymmetricSourceKind.CircularOgive => new LaserCollisionIn3DObjects.Domain.Geometry.AxisymmetricSourceProfileDefinition
            {
                Kind = AxisymmetricSourceKind.CircularOgive,
                RadiusStart = (float)_appliedGeometry.RadiusStart,
                RadiusEnd = (float)_appliedGeometry.RadiusEnd,
                Length = (float)_appliedGeometry.Length,
                ArcRadius = (float)_appliedGeometry.ArcRadius,
                OgiveCurvatureDirection = _appliedGeometry.Curvature,
            },
            AxisymmetricSourceKind.Hybrid => new LaserCollisionIn3DObjects.Domain.Geometry.AxisymmetricSourceProfileDefinition
            {
                Kind = AxisymmetricSourceKind.Hybrid,
                Length = _appliedGeometry.HybridSegments.Sum(segment => segment.Length),
                Hybrid = _appliedGeometry.HybridSegments.Select(segment => new HybridAxisymmetricSourceSegmentDefinition(
                    segment.Kind,
                    segment.Length,
                    segment.RadiusStart,
                    segment.RadiusEnd,
                    segment.Kind == HybridAxisymmetricSourceSegmentKind.CircularOgive ? segment.ArcRadius : null,
                    segment.Curvature)).ToList(),
            },
            _ => throw new InvalidOperationException($"Unsupported projection geometry kind '{_appliedGeometry.Kind}'.")
        };
    }

    private IAxisymmetricSourceProfile? BuildSelectedProjectionGeometryProfile()
    {
        try
        {
            return _appliedGeometry.Kind switch
            {
                AxisymmetricSourceKind.Cylinder => new AxisymmetricSourceProfile((float)_appliedGeometry.RadiusStart, (float)_appliedGeometry.Length),
                AxisymmetricSourceKind.ConicalFrustum => new ConicalFrustumSourceProfile((float)_appliedGeometry.RadiusStart, (float)_appliedGeometry.RadiusEnd, (float)_appliedGeometry.Length),
                AxisymmetricSourceKind.CircularOgive => new CircularOgiveSourceProfile((float)_appliedGeometry.RadiusStart, (float)_appliedGeometry.RadiusEnd, (float)_appliedGeometry.Length, (float)_appliedGeometry.ArcRadius, _appliedGeometry.Curvature),
                AxisymmetricSourceKind.Hybrid => BuildHybridPreviewProfile(),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private IAxisymmetricSourceProfile? BuildHybridPreviewProfile()
    {
        if (_appliedGeometry.HybridSegments.Length == 0)
        {
            return null;
        }

        try
        {
            return new HybridAxisymmetricSourceProfile(_appliedGeometry.HybridSegments.Select(segment => new HybridAxisymmetricSourceSegmentDefinition(
                segment.Kind,
                segment.Length,
                segment.RadiusStart,
                segment.RadiusEnd,
                segment.Kind == HybridAxisymmetricSourceSegmentKind.CircularOgive ? segment.ArcRadius : null,
                segment.Curvature)).ToList());
        }
        catch
        {
            return null;
        }
    }


    private Frame3D BuildPreviewFrame()
    {
        var sourceFrame = PointSourceFrameBuilder.Build(
            _appliedGeometry.Origin,
            _appliedGeometry.FrameX,
            _appliedGeometry.FrameY);

        var x = new System.Numerics.Vector3((float)sourceFrame.AxisX.X, (float)sourceFrame.AxisX.Y, (float)sourceFrame.AxisX.Z);
        var y = new System.Numerics.Vector3((float)sourceFrame.AxisY.X, (float)sourceFrame.AxisY.Y, (float)sourceFrame.AxisY.Z);
        var z = new System.Numerics.Vector3((float)sourceFrame.AxisZ.X, (float)sourceFrame.AxisZ.Y, (float)sourceFrame.AxisZ.Z);
        var matrix = new System.Numerics.Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            0, 0, 0, 1);
        var orientation = System.Numerics.Quaternion.CreateFromRotationMatrix(matrix);
        return new Frame3D(new System.Numerics.Vector3((float)sourceFrame.Origin.X, (float)sourceFrame.Origin.Y, (float)sourceFrame.Origin.Z), orientation);
    }

    private async Task AddProjectedLightSourceToCollisionAsync()
    {
        if (SelectedScene is null)
        {
            SetStatus("Select a projection scene first.", ApplicationLogLevel.Warning);
            return;
        }

        var selectedResult = SelectedResult;
        if (selectedResult is null)
        {
            SetStatus("Run or select a projection result first.", ApplicationLogLevel.Warning);
            return;
        }

        var targetScene = SelectedTargetCollisionScene;
        if (targetScene is null)
        {
            SetStatus("Select a target collision scene before adding the projected light source.", ApplicationLogLevel.Warning);
            return;
        }

        if (targetScene.IsProjectionOnly)
        {
            SetStatus("Target scene must be a collision scene, not a projection-only scene.", ApplicationLogLevel.Warning);
            return;
        }


        _isBridgeBusy = true;
        RaisePropertyChanged(nameof(IsProjectionBusy));
        RaisePropertyChanged(nameof(IsProgressVisible));
        ProjectionProgressMessage = "Preparing exact projected rays...";
        RaiseCanExecuteChanged();
        try
        {
            var projectedSource = await Task.Run(() => _projectionResultToCollisionSourceService.CreateProjectedLightSource(selectedResult));
            ProjectionProgressMessage = "Adding source to collision scene...";
            targetScene.InvalidateCollisionResults();
            targetScene.ProjectedLightSources.Add(projectedSource);
            targetScene.SelectedProjectedLightSource = projectedSource;
            if (!ReferenceEquals(_sceneCollectionService.SelectedScene, targetScene)) _sceneCollectionService.SelectedScene = targetScene;
            _sceneCollectionService.NotifySceneContentChanged();
            SetStatus(
                $"Added projected light source '{projectedSource.Name}' to collision scene '{targetScene.Name}' with {projectedSource.Rays.Count} exact ray(s).",
                ApplicationLogLevel.Success);
            _applicationLogService?.LogSuccess($"Added projected light source '{projectedSource.Name}' with {projectedSource.Rays.Count} exact rays to '{targetScene.Name}'.", nameof(ProjectionWorkspaceViewModel));
            RaiseCanExecuteChanged();
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(ex.Message, ApplicationLogLevel.Warning, ex);
            _applicationLogService?.LogWarning(ex.Message, nameof(ProjectionWorkspaceViewModel));
        }
        finally
        {
            _isBridgeBusy = false;
            ProjectionProgressMessage = string.Empty;
            RaisePropertyChanged(nameof(IsProjectionBusy));
            RaisePropertyChanged(nameof(IsProgressVisible));
            RaiseCanExecuteChanged();
        }
    }

    private bool CanAddProjectedLightSourceToCollision() =>
        !IsProjectionBusy
        && SelectedResult is not null
        && SelectedTargetCollisionScene is not null
        && !SelectedTargetCollisionScene.IsProjectionOnly;

    private static System.Numerics.Quaternion BuildOrientationFromFrame(PointSourceFrameState sourceFrame)
    {
        var x = new System.Numerics.Vector3((float)sourceFrame.AxisX.X, (float)sourceFrame.AxisX.Y, (float)sourceFrame.AxisX.Z);
        var y = new System.Numerics.Vector3((float)sourceFrame.AxisY.X, (float)sourceFrame.AxisY.Y, (float)sourceFrame.AxisY.Z);
        var z = new System.Numerics.Vector3((float)sourceFrame.AxisZ.X, (float)sourceFrame.AxisZ.Y, (float)sourceFrame.AxisZ.Z);
        var matrix = new System.Numerics.Matrix4x4(
            x.X, x.Y, x.Z, 0,
            y.X, y.Y, y.Z, 0,
            z.X, z.Y, z.Z, 0,
            0, 0, 0, 1);
        return System.Numerics.Quaternion.CreateFromRotationMatrix(matrix);
    }

    private void RaiseCanExecuteChanged()
    {
        (RunProjectionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ApplyGeometryChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();

        if (DeleteSelectedResultCommand is RelayCommand deleteSelectedResultCommand)
        {
            deleteSelectedResultCommand.RaiseCanExecuteChanged();
        }

        if (DeleteSelectedSceneCommand is RelayCommand deleteSelectedSceneCommand)
        {
            deleteSelectedSceneCommand.RaiseCanExecuteChanged();
        }

        if (AddProjectedLightSourceToCollisionSceneCommand is AsyncRelayCommand addProjectedLightSourceToCollisionSceneCommand)
        {
            addProjectedLightSourceToCollisionSceneCommand.RaiseCanExecuteChanged();
        }

        if (RemoveSelectedHybridSegmentCommand is RelayCommand removeSelectedHybridSegmentCommand)
        {
            removeSelectedHybridSegmentCommand.RaiseCanExecuteChanged();
        }

        (ImportHitPointsCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddHybridSegmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnSavedResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(SavedResults));
        RaisePropertyChanged(nameof(SelectedResult));
        RaiseCanExecuteChanged();
    }

    private sealed record HybridSegmentSnapshot(HybridAxisymmetricSourceSegmentKind Kind, float Length, float RadiusStart, float RadiusEnd, float ArcRadius, OgiveCurvatureDirection Curvature);

    private sealed record ProjectionGeometrySnapshot(
        AxisymmetricSourceKind Kind, double BeamOriginX, double BeamOriginY, double BeamOriginZ,
        double SourceFrameXx, double SourceFrameXy, double SourceFrameXz,
        double SourceFrameYx, double SourceFrameYy, double SourceFrameYz,
        double RadiusStart, double RadiusEnd, double Length, double ArcRadius, OgiveCurvatureDirection Curvature,
        HybridSegmentSnapshot[] HybridSegments)
    {
        public Point3 Origin => new(BeamOriginX, BeamOriginY, BeamOriginZ);
        public Vector3D FrameX => new(SourceFrameXx, SourceFrameXy, SourceFrameXz);
        public Vector3D FrameY => new(SourceFrameYx, SourceFrameYy, SourceFrameYz);
    }

}
