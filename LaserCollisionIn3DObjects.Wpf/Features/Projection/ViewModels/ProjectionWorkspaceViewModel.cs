using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;

public sealed class ProjectionWorkspaceViewModel : ObservableObject
{
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly ProjectionRenderSyncService _projectionRenderSyncService;
    private ProjectionMethodOptionViewModel? _selectedMethod;
    private CollisionSceneViewModel? _selectedScene;
    private string _statusMessage = "Select a scene with holes to begin projection.";

    public ProjectionWorkspaceViewModel(
        SceneCollectionService sceneCollectionService,
        ProjectionRenderSyncService projectionRenderSyncService,
        IEnumerable<IProjectionMethod>? projectionMethods = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _projectionRenderSyncService = projectionRenderSyncService ?? throw new ArgumentNullException(nameof(projectionRenderSyncService));

        var methods = projectionMethods?.ToList() ?? new List<IProjectionMethod> { new PointSourceProjectionMethod() };
        ProjectionMethods = new ObservableCollection<ProjectionMethodOptionViewModel>(
            methods.Select(method => new ProjectionMethodOptionViewModel { Method = method }));

        _selectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == ProjectionWorkspaceState.DefaultMethodId)
            ?? ProjectionMethods.FirstOrDefault();

        RunProjectionCommand = new RelayCommand(RunProjection, CanRunProjection);

        _sceneCollectionService.Scenes.CollectionChanged += OnScenesCollectionChanged;
        RefreshAvailableScenes();
    }

    public ObservableCollection<ProjectionMethodOptionViewModel> ProjectionMethods { get; }

    public ObservableCollection<CollisionSceneViewModel> AvailableScenes { get; } = new();

    public ICommand RunProjectionCommand { get; }

    public double SourceX { get; set; }

    public double SourceY { get; set; }

    public double SourceZ { get; set; }

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
            RefreshViewport();
            RaiseCanExecuteChanged();
        }
    }

    public CollisionSceneViewModel? SelectedScene
    {
        get => _selectedScene;
        set
        {
            if (!SetProperty(ref _selectedScene, value))
            {
                return;
            }

            if (value is not null)
            {
                SelectedMethod = ProjectionMethods.FirstOrDefault(method => method.Id == value.ProjectionState.SelectedMethodId)
                    ?? ProjectionMethods.FirstOrDefault();
            }

            RefreshViewport();
            RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private bool CanRunProjection()
    {
        return SelectedScene is not null && SelectedScene.HolePoints.Count > 0 && SelectedMethod is not null;
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

        try
        {
            var request = new ProjectionRequest
            {
                HolePoints = scene.HolePoints.ToList(),
                Parameters = BuildParameters(SelectedMethod.Method),
            };

            var result = SelectedMethod.Method.Execute(request);
            SceneProjectionStateUpdater.Apply(scene.ProjectionState, result);

            RefreshViewport();
            StatusMessage = $"Projection completed. Generated {result.Rays.Count} ray(s) using '{SelectedMethod.DisplayName}'.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            StatusMessage = ex.Message;
        }
    }

    private IProjectionParameters BuildParameters(IProjectionMethod method)
    {
        if (method.Metadata.Id == ProjectionMethodIds.PointSource)
        {
            return new PointSourceProjectionParameters(new Point3(SourceX, SourceY, SourceZ));
        }

        throw new InvalidOperationException($"Projection method '{method.Metadata.Id}' is not yet supported by the workspace UI parameter panel.");
    }

    private void RefreshViewport()
    {
        var scene = SelectedScene;
        var holePoints = scene?.HolePoints?.ToList() ?? new List<Point3>();
        var result = scene?.ProjectionState.LastResult;
        _projectionRenderSyncService.SyncProjectionScene(holePoints, result);
    }

    private void OnScenesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAvailableScenes();
    }

    private void RefreshAvailableScenes()
    {
        var selected = SelectedScene;

        AvailableScenes.Clear();
        foreach (var scene in _sceneCollectionService.Scenes.Where(scene => scene.HolePoints.Count > 0))
        {
            AvailableScenes.Add(scene);
        }

        if (selected is not null && AvailableScenes.Contains(selected))
        {
            SelectedScene = selected;
        }
        else
        {
            SelectedScene = AvailableScenes.FirstOrDefault();
        }

        RaiseCanExecuteChanged();
    }

    private void RaiseCanExecuteChanged()
    {
        if (RunProjectionCommand is RelayCommand runProjectionCommand)
        {
            runProjectionCommand.RaiseCanExecuteChanged();
        }
    }
}
