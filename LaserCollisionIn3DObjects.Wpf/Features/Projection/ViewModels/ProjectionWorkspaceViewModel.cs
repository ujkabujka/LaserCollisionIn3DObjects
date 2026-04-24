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
    private ProjectionMethodOptionViewModel? _selectedMethod;
    private CollisionSceneViewModel? _selectedScene;
    private string _statusMessage = "Select a scene with holes to begin projection.";
    private string _newResultName = "Projection Result 1";

    public ProjectionWorkspaceViewModel(
        SceneCollectionService sceneCollectionService,
        ProjectionRenderSyncService projectionRenderSyncService,
        ProjectionMethodRegistry? methodRegistry = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _projectionRenderSyncService = projectionRenderSyncService ?? throw new ArgumentNullException(nameof(projectionRenderSyncService));
        _methodRegistry = methodRegistry ?? new ProjectionMethodRegistry(new[] { new PointSourceProjectionMethod() });

        ProjectionMethods = new ObservableCollection<ProjectionMethodOptionViewModel>(
            _methodRegistry.Methods.Select(method => new ProjectionMethodOptionViewModel { Method = method }));

        _selectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == ProjectionWorkspaceState.DefaultMethodId)
            ?? ProjectionMethods.FirstOrDefault();

        RunProjectionCommand = new RelayCommand(RunProjection, CanRunProjection);
        ImportHitPointsCsvCommand = new RelayCommand(ImportHitPointsCsv);
        DeleteSelectedResultCommand = new RelayCommand(DeleteSelectedResult, () => SelectedResult is not null);

        _sceneCollectionService.Scenes.CollectionChanged += OnScenesCollectionChanged;
        RefreshAvailableScenes();
    }

    public ObservableCollection<ProjectionMethodOptionViewModel> ProjectionMethods { get; }

    public ObservableCollection<CollisionSceneViewModel> AvailableScenes { get; } = new();

    public ICommand RunProjectionCommand { get; }
    public ICommand ImportHitPointsCsvCommand { get; }
    public ICommand DeleteSelectedResultCommand { get; }

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
        SelectedScene is not null && SelectedScene.HolePoints.Count > 0 && SelectedMethod is not null;

    private void ImportHitPointsCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog() != true)
        {
            StatusMessage = "CSV import canceled.";
            return;
        }

        ProjectionHitPointCsvImportResult importResult;
        try
        {
            importResult = _projectionHitPointCsvImportService.Import(dialog.FileName);
        }
        catch (ArgumentException ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        if (importResult.HolePoints.Count == 0)
        {
            StatusMessage = "No valid hit-point rows were found in the selected CSV.";
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

        StatusMessage = $"Imported {importResult.HolePoints.Count} hole points into projection scene '{sceneName}'. Skipped {importResult.SkippedRowCount} invalid rows.";
    }

    private void RunProjection()
    {
        var scene = SelectedScene;
        if (scene is null)
        {
            StatusMessage = "Select a scene with holes before running projection.";
            return;
        }

        if (SelectedMethod is null)
        {
            StatusMessage = "Select a projection methodology.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewResultName))
        {
            StatusMessage = "Provide a projection result name.";
            return;
        }

        try
        {
            var request = new ProjectionRequest
            {
                HolePoints = scene.HolePoints.ToList(),
                Parameters = BuildParameters(SelectedMethod.Method),
            };

            var result = SelectedMethod.Method.Execute(request);
            var namedResult = SceneProjectionStateUpdater.SaveResult(scene.ProjectionState, NewResultName, result);
            NewResultName = $"Projection Result {scene.ProjectionState.SavedResults.Count + 1}";
            scene.ProjectionState.SelectedMethodId = SelectedMethod.Id;
            SelectedResult = namedResult;

            StatusMessage = $"Projection completed and saved as '{namedResult.DisplayName}' ({result.Rays.Count} ray(s)).";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            StatusMessage = ex.Message;
        }
    }

    private void DeleteSelectedResult()
    {
        var scene = SelectedScene;
        var selectedResult = SelectedResult;
        if (scene is null || selectedResult is null)
        {
            StatusMessage = "Select a projection result to delete.";
            return;
        }

        var deleted = SceneProjectionStateUpdater.DeleteResult(scene.ProjectionState, selectedResult);
        if (!deleted)
        {
            StatusMessage = "Selected projection result could not be deleted.";
            return;
        }

        RaisePropertyChanged(nameof(SavedResults));
        RaisePropertyChanged(nameof(SelectedResult));
        RefreshViewport();
        RaiseCanExecuteChanged();
        StatusMessage = $"Deleted projection result '{selectedResult.DisplayName}'.";
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

        throw new InvalidOperationException($"Projection method '{method.Metadata.Id}' is not yet supported by the workspace UI parameter panel.");
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
    }

    private void OnSavedResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(SavedResults));
        RaisePropertyChanged(nameof(SelectedResult));
        RaiseCanExecuteChanged();
    }
}
