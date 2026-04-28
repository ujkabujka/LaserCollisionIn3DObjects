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
    private ProjectionMethodOptionViewModel? _selectedMethod;
    private CollisionSceneViewModel? _selectedScene;
    private string _statusMessage = "Select a scene with holes to begin projection.";
    private string _newResultName = "Projection Result 1";
    private bool _isProjectionRunning;
    private double _projectionProgressPercent;
    private string _projectionProgressMessage = string.Empty;
    private int _lastLoggedProgressBucket = -1;

    public ProjectionWorkspaceViewModel(
        SceneCollectionService sceneCollectionService,
        ProjectionRenderSyncService projectionRenderSyncService,
        ProjectionMethodRegistry? methodRegistry = null,
        ApplicationLogService? applicationLogService = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _projectionRenderSyncService = projectionRenderSyncService ?? throw new ArgumentNullException(nameof(projectionRenderSyncService));
        _methodRegistry = methodRegistry ?? new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new CylindricalSourceProjectionMethod(),
            new SelfCalibratingCylindricalProjectionMethod(),
        });
        _applicationLogService = applicationLogService;

        ProjectionMethods = new ObservableCollection<ProjectionMethodOptionViewModel>(
            _methodRegistry.Methods.Select(method => new ProjectionMethodOptionViewModel { Method = method }));

        _selectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == ProjectionWorkspaceState.DefaultMethodId)
            ?? ProjectionMethods.FirstOrDefault();

        RunProjectionCommand = new RelayCommand(() => _ = RunProjectionAsync(), CanRunProjection);
        ImportHitPointsCsvCommand = new RelayCommand(ImportHitPointsCsv);
        DeleteSelectedResultCommand = new RelayCommand(DeleteSelectedResult, () => SelectedResult is not null);
        DeleteSelectedProjectionSceneCommand = new RelayCommand(DeleteSelectedProjectionScene, () => CanDeleteSelectedProjectionScene);

        _sceneCollectionService.Scenes.CollectionChanged += OnScenesCollectionChanged;
        RefreshAvailableScenes();
    }

    public ObservableCollection<ProjectionMethodOptionViewModel> ProjectionMethods { get; }

    public ObservableCollection<CollisionSceneViewModel> AvailableScenes { get; } = new();

    public ICommand RunProjectionCommand { get; }
    public ICommand ImportHitPointsCsvCommand { get; }
    public ICommand DeleteSelectedResultCommand { get; }
    public ICommand DeleteSelectedProjectionSceneCommand { get; }

    public double PointSourceX { get; set; }
    public double PointSourceY { get; set; }
    public double PointSourceZ { get; set; }

    public double BeamOriginX { get; set; }
    public double BeamOriginY { get; set; }
    public double BeamOriginZ { get; set; }

    public double SourceFrameXx { get; set; } = 1;
    public double SourceFrameXy { get; set; }
    public double SourceFrameXz { get; set; }

    public double SourceFrameYx { get; set; }
    public double SourceFrameYy { get; set; } = 1;
    public double SourceFrameYz { get; set; }

    public double CylindricalRadius { get; set; } = 1;
    public double CylindricalLength { get; set; } = 10;
    public double TiltPointX { get; set; }
    public double TiltPointY { get; set; }
    public double TiltPointZ { get; set; }

    public bool IsPointSourceMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.PointSource, StringComparison.OrdinalIgnoreCase);
    public bool IsLegacyCylindricalMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.CylindricalSource, StringComparison.OrdinalIgnoreCase);
    public bool IsSelfCalibratingCylindricalMethodSelected => string.Equals(SelectedMethod?.Id, ProjectionMethodIds.SelfCalibratingCylindricalSource, StringComparison.OrdinalIgnoreCase);
    public bool IsAnyCylindricalMethodSelected => IsLegacyCylindricalMethodSelected || IsSelfCalibratingCylindricalMethodSelected;

    public bool IsProjectionRunning
    {
        get => _isProjectionRunning;
        private set
        {
            if (SetProperty(ref _isProjectionRunning, value))
            {
                RaisePropertyChanged(nameof(IsProgressVisible));
                RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsProgressVisible => IsProjectionRunning;

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

    public bool CanDeleteSelectedProjectionScene => SelectedScene?.IsProjectionOnly == true;

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
            RaisePropertyChanged(nameof(IsLegacyCylindricalMethodSelected));
            RaisePropertyChanged(nameof(IsSelfCalibratingCylindricalMethodSelected));
            RaisePropertyChanged(nameof(IsAnyCylindricalMethodSelected));
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
            RaisePropertyChanged(nameof(CanDeleteSelectedProjectionScene));
            RefreshViewport();
            RaiseCanExecuteChanged();
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
            SelectedMethodId = SelectedMethod?.Id ?? ProjectionWorkspaceState.DefaultMethodId,
        };
    }

    public void ApplyWorkspaceState(ProjectionWorkspaceStateDto state)
    {
        ArgumentNullException.ThrowIfNull(state);

        SelectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == state.SelectedMethodId)
            ?? ProjectionMethods.FirstOrDefault(method => method.Id == ProjectionWorkspaceState.DefaultMethodId)
            ?? ProjectionMethods.FirstOrDefault();

        SelectedScene = AvailableScenes.FirstOrDefault(scene => scene.Name == state.SelectedSceneName)
            ?? AvailableScenes.FirstOrDefault();
    }

    private bool CanRunProjection() =>
        !IsProjectionRunning && SelectedScene is not null && SelectedScene.HolePoints.Count > 0 && SelectedMethod is not null;

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
        RefreshAvailableScenes();
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

        if (string.IsNullOrWhiteSpace(NewResultName))
        {
            SetStatus("Provide a projection result name.", ApplicationLogLevel.Warning);
            return;
        }

        IsProjectionRunning = true;
        ProjectionProgressPercent = 0;
        ProjectionProgressMessage = "Preparing projection...";
        _lastLoggedProgressBucket = -1;
        _applicationLogService?.LogInfo($"Projection scene: {scene.Name}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Projection method: {SelectedMethod.DisplayName} ({SelectedMethod.Id})", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo($"Projection input hole points: {scene.HolePoints.Count}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService?.LogInfo("Projection run started.", nameof(ProjectionWorkspaceViewModel));

        try
        {
            var progress = new Progress<ProjectionProgress>(report =>
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
            });

            var request = new ProjectionRequest
            {
                HolePoints = scene.HolePoints.ToList(),
                Parameters = BuildParameters(SelectedMethod.Method),
                Progress = progress,
            };

            var method = SelectedMethod.Method;
            var result = await Task.Run(() => method.Execute(request));
            var namedResult = SceneProjectionStateUpdater.SaveResult(scene.ProjectionState, NewResultName, result);
            NewResultName = $"Projection Result {scene.ProjectionState.SavedResults.Count + 1}";
            scene.ProjectionState.SelectedMethodId = SelectedMethod.Id;
            SelectedResult = namedResult;

            SetStatus(result.CylindricalSource is null
                ? $"Projection completed and saved as '{namedResult.DisplayName}' ({result.Rays.Count} ray(s))."
                : $"Cylindrical projection completed and saved as '{namedResult.DisplayName}' ({result.CylindricalSource.Points.Count} reconstructed source points).",
                ApplicationLogLevel.Success);
            LogProjectionSummary(result);
            _applicationLogService?.LogSuccess("Projection run completed.", nameof(ProjectionWorkspaceViewModel));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
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

    private void DeleteSelectedProjectionScene()
    {
        if (SelectedScene is null)
        {
            SetStatus("Select a scene to delete.", ApplicationLogLevel.Warning);
            return;
        }

        if (!SelectedScene.IsProjectionOnly)
        {
            SetStatus("Only projection-only scenes can be deleted from Projection Workspace.", ApplicationLogLevel.Warning);
            return;
        }

        var deletedName = SelectedScene.Name;
        _sceneCollectionService.RemoveScene(SelectedScene);
        RefreshAvailableScenes();
        RefreshViewport();
        SetStatus($"Deleted projection scene '{deletedName}'.", ApplicationLogLevel.Success);
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
                new Point3(BeamOriginX, BeamOriginY, BeamOriginZ),
                new Vector3D(SourceFrameXx, SourceFrameXy, SourceFrameXz),
                new Vector3D(SourceFrameYx, SourceFrameYy, SourceFrameYz));
        }

        if (method.Metadata.Id == ProjectionMethodIds.CylindricalSource)
        {
            return new CylindricalSourceProjectionParameters(
                new Point3(BeamOriginX, BeamOriginY, BeamOriginZ),
                new Vector3D(SourceFrameXx, SourceFrameXy, SourceFrameXz),
                new Vector3D(SourceFrameYx, SourceFrameYy, SourceFrameYz),
                CylindricalRadius,
                CylindricalLength);
        }

        if (method.Metadata.Id == ProjectionMethodIds.SelfCalibratingCylindricalSource)
        {
            return new SelfCalibratingCylindricalProjectionParameters(
                new Point3(BeamOriginX, BeamOriginY, BeamOriginZ),
                new Vector3D(SourceFrameXx, SourceFrameXy, SourceFrameXz),
                new Vector3D(SourceFrameYx, SourceFrameYy, SourceFrameYz),
                CylindricalRadius,
                CylindricalLength,
                new Point3(TiltPointX, TiltPointY, TiltPointZ));
        }

        throw new InvalidOperationException($"Projection method '{method.Metadata.Id}' is not yet supported by the workspace UI parameter panel.");
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

        var cylindrical = result.CylindricalSource;
        if (cylindrical is null)
        {
            return;
        }

        _applicationLogService.LogInfo($"Radius: {cylindrical.Radius:F6}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService.LogInfo($"Length: {cylindrical.Length:F6}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService.LogInfo($"Reconstructed source points: {cylindrical.Points.Count}", nameof(ProjectionWorkspaceViewModel));

        var fitEntries = cylindrical.Points
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

        if (result.MethodId != ProjectionMethodIds.SelfCalibratingCylindricalSource)
        {
            return;
        }

        _applicationLogService.LogSuccess("Self-calibrating cylindrical projection completed.", nameof(ProjectionWorkspaceViewModel));
        if (cylindrical.EstimatedTiltWeight.HasValue)
        {
            _applicationLogService.LogSuccess($"Estimated lambda: {cylindrical.EstimatedTiltWeight.Value:F6}", nameof(ProjectionWorkspaceViewModel));
        }

        if (cylindrical.Diagnostics is null)
        {
            return;
        }

        _applicationLogService.LogInfo($"Regularity weight: {cylindrical.Diagnostics.RegularityWeight:F6}", nameof(ProjectionWorkspaceViewModel));
        _applicationLogService.LogInfo($"Candidates evaluated: {cylindrical.Diagnostics.CandidateScores.Count}", nameof(ProjectionWorkspaceViewModel));

        var bestCandidate = cylindrical.Diagnostics.CandidateScores.MinBy(candidate => candidate.Score);
        if (bestCandidate is not null)
        {
            _applicationLogService.LogInfo($"Best candidate score: {bestCandidate.Score:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Best candidate regularity score: {bestCandidate.RegularityError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Best candidate mean fit error: {bestCandidate.MeanFitError:F6}", nameof(ProjectionWorkspaceViewModel));
            _applicationLogService.LogInfo($"Best candidate total score: {bestCandidate.Score:F6}", nameof(ProjectionWorkspaceViewModel));
        }
    }

    private void RefreshViewport()
    {
        var scene = SelectedScene;
        var holePoints = scene?.HolePoints?.ToList() ?? new List<Point3>();
        var result = scene?.ProjectionState.SelectedResult;
        _projectionRenderSyncService.SyncProjectionScene(holePoints, result);
    }

    private void OnScenesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshAvailableScenes();

    private void RefreshAvailableScenes()
    {
        var selected = SelectedScene;

        AvailableScenes.Clear();
        foreach (var scene in _sceneCollectionService.Scenes.Where(scene => scene.HolePoints.Count > 0))
        {
            AvailableScenes.Add(scene);
        }

        SelectedScene = selected is not null && AvailableScenes.Contains(selected)
            ? selected
            : AvailableScenes.FirstOrDefault();

        RaiseCanExecuteChanged();
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

    private void RaiseCanExecuteChanged()
    {
        if (RunProjectionCommand is RelayCommand runProjectionCommand)
        {
            runProjectionCommand.RaiseCanExecuteChanged();
        }

        if (DeleteSelectedResultCommand is RelayCommand deleteSelectedResultCommand)
        {
            deleteSelectedResultCommand.RaiseCanExecuteChanged();
        }

        if (DeleteSelectedProjectionSceneCommand is RelayCommand deleteSelectedSceneCommand)
        {
            deleteSelectedSceneCommand.RaiseCanExecuteChanged();
        }
    }

    private void OnSavedResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(SavedResults));
        RaisePropertyChanged(nameof(SelectedResult));
        RaiseCanExecuteChanged();
    }
}
