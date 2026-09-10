using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using LaserCollisionIn3DObjects.Domain.SourceCompletion;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.Features.Projection.ViewModels;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.SourceCompletion.ViewModels;

public sealed class SourceCompletionWorkspaceViewModel : ObservableObject
{
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly ApplicationLogService? _applicationLogService;
    private readonly ProjectedSourceAzimuthAnalyzer _azimuthAnalyzer = new();
    private readonly ProjectedSourceCompletionService _completionService = new();
    private readonly ProjectionWorkspaceViewModel? _projectionWorkspace;
    private readonly CompletedSourceStore _completedSourceStore;
    private readonly ProjectionResultToCollisionSourceService _projectionResultToCollisionSourceService = new();
    private SourceCompletionPreviewRenderSyncService? _previewRenderSyncService;
    private SourceCompletionInputItem? _selectedProjectedSourceInput;
    private CollisionSceneViewModel? _selectedTargetCollisionScene;
    private double _angularStepDegrees = 5d;
    private double _gapThresholdDegrees = 10d;
    private bool _includeOriginalRays = true;
    private string _maxSyntheticRaysText = string.Empty;
    private SourceCompletionMethod _selectedCompletionMethod = SourceCompletionMethod.RotationalCopy;
    private double _mirrorAxisDegrees;
    private ProjectedSourceCompletionResult? _lastCompletionResult;
    private string _statusMessage = "Select a projected source to analyze.";
    private string _completionSummary = "No completed source generated yet.";
    private bool _isBusy;
    private string _progressMessage = string.Empty;

    public SourceCompletionWorkspaceViewModel(SceneCollectionService sceneCollectionService, CompletedSourceStore completedSourceStore, ProjectionWorkspaceViewModel? projectionWorkspace = null, ApplicationLogService? applicationLogService = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _applicationLogService = applicationLogService;
        _completedSourceStore = completedSourceStore ?? throw new ArgumentNullException(nameof(completedSourceStore));
        _projectionWorkspace = projectionWorkspace;

        RefreshSourcesCommand = new RelayCommand(RefreshSources, () => !IsBusy);
        AnalyzeCoverageCommand = new AsyncRelayCommand(AnalyzeCoverageAsync, CanAnalyzeOrGenerate);
        GenerateCompletedSourceCommand = new AsyncRelayCommand(GenerateCompletedSourceAsync, CanAnalyzeOrGenerate);
        AddCompletedSourceToCollisionCommand = new AsyncRelayCommand(AddCompletedSourceToCollisionAsync, CanAddCompletedSource);
        RemoveSelectedCompletedSourceCommand = new RelayCommand(RemoveSelectedCompletedSource, () => !IsBusy && SelectedCompletedSource is not null);

        _sceneCollectionService.Scenes.CollectionChanged += (_, _) => RefreshSources();
        _sceneCollectionService.SceneContentChanged += (_, _) => RefreshSources();
        RefreshSources();
    }

    public ObservableCollection<SourceCompletionInputItem> AvailableProjectedSources { get; } = new();
    public ObservableCollection<CollisionSceneViewModel> TargetCollisionScenes { get; } = new();
    public ObservableCollection<AzimuthCoverageInterval> CoverageIntervals { get; } = new();
    public ObservableCollection<AzimuthGapInterval> GapIntervals { get; } = new();
    public ObservableCollection<SourceCompletionMethod> CompletionMethods { get; } = new(Enum.GetValues<SourceCompletionMethod>());
    public ObservableCollection<CompletedSourceItem> CompletedSources => _completedSourceStore.CompletedSources;
    public CompletedSourceItem? SelectedCompletedSource { get => _completedSourceStore.SelectedItem; set { _completedSourceStore.SelectedItem = value; RaiseCommandStates(); RefreshPreview(); } }
    public string SelectedCompletedSourceName { get => SelectedCompletedSource?.Name ?? string.Empty; set { if (SelectedCompletedSource is not null) { SelectedCompletedSource.Name = value; RaisePropertyChanged(); } } }

    public SourceCompletionInputItem? SelectedProjectedSourceInput
    {
        get => _selectedProjectedSourceInput;
        set
        {
            var logicalChange = !string.Equals(_selectedProjectedSourceInput?.StableId, value?.StableId, StringComparison.Ordinal);
            if (SetProperty(ref _selectedProjectedSourceInput, value))
            {
                if (logicalChange) InvalidateTransientState();
                RaisePropertyChanged(nameof(SelectedProjectedSource));
                RaiseCommandStates();
                RefreshPreview();
            }
        }
    }

    public ProjectedLightSourceItemViewModel? SelectedProjectedSource => SelectedProjectedSourceInput?.Source;

    public CollisionSceneViewModel? SelectedTargetCollisionScene
    {
        get => _selectedTargetCollisionScene;
        set
        {
            if (SetProperty(ref _selectedTargetCollisionScene, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public double AngularStepDegrees { get => _angularStepDegrees; set => SetProperty(ref _angularStepDegrees, value); }
    public double GapThresholdDegrees { get => _gapThresholdDegrees; set => SetProperty(ref _gapThresholdDegrees, value); }
    public bool IncludeOriginalRays { get => _includeOriginalRays; set => SetProperty(ref _includeOriginalRays, value); }
    public string MaxSyntheticRaysText { get => _maxSyntheticRaysText; set => SetProperty(ref _maxSyntheticRaysText, value); }
    public SourceCompletionMethod SelectedCompletionMethod { get => _selectedCompletionMethod; set => SetProperty(ref _selectedCompletionMethod, value); }
    public double MirrorAxisDegrees { get => _mirrorAxisDegrees; set => SetProperty(ref _mirrorAxisDegrees, value); }

    public ProjectedSourceCompletionResult? LastCompletionResult
    {
        get => _lastCompletionResult;
        private set
        {
            if (SetProperty(ref _lastCompletionResult, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string CompletionSummary { get => _completionSummary; private set => SetProperty(ref _completionSummary, value); }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseCommandStates(); } }
    public bool IsProgressVisible => IsBusy;
    public string ProgressMessage { get => _progressMessage; private set => SetProperty(ref _progressMessage, value); }

    public ICommand RefreshSourcesCommand { get; }
    public ICommand AnalyzeCoverageCommand { get; }
    public ICommand GenerateCompletedSourceCommand { get; }
    public ICommand AddCompletedSourceToCollisionCommand { get; }
    public ICommand RemoveSelectedCompletedSourceCommand { get; }

    private void RefreshSources()
    {
        var selectedStableId = SelectedProjectedSourceInput?.StableId;
        AvailableProjectedSources.Clear();
        TargetCollisionScenes.Clear();

        foreach (var scene in _sceneCollectionService.Scenes)
        {
            if (!scene.IsProjectionOnly)
            {
                TargetCollisionScenes.Add(scene);
            }

        }

        PopulateProjectionResultInputs();

        SelectedProjectedSourceInput = AvailableProjectedSources.FirstOrDefault(item => item.StableId == selectedStableId)
            ?? AvailableProjectedSources.FirstOrDefault();

        if (SelectedTargetCollisionScene is null || !TargetCollisionScenes.Contains(SelectedTargetCollisionScene))
        {
            SelectedTargetCollisionScene = TargetCollisionScenes.FirstOrDefault();
        }

        RaiseCommandStates();
        RefreshPreview();
    }

    private ProjectedSourceCompletionRequest BuildRequest(ProjectedLightSourceItemViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Rays.Count == 0)
        {
            throw new InvalidOperationException("Selected projected source does not contain rays.");
        }

        return new ProjectedSourceCompletionRequest(source.Name, source.ProfileDefinition, source.SourceFrame, source.Rays.ToList());
    }

    private async Task AnalyzeCoverageAsync()
    {
        var cancellationToken = (System.Windows.Application.Current as App)?.Lifetime.Token ?? CancellationToken.None;
        if (cancellationToken.IsCancellationRequested) return;
        if (SelectedProjectedSource is null || SelectedProjectedSource.Rays.Count == 0 || GapThresholdDegrees <= 0d)
        {
            StatusMessage = SelectedProjectedSource is null
                ? "Select a projected light source first."
                : SelectedProjectedSource.Rays.Count == 0
                    ? "The selected projected light source has zero rays. Choose another source."
                    : "Gap threshold must be greater than 0 degrees.";
            return;
        }

        var request = BuildRequest(SelectedProjectedSource);
        var threshold = GapThresholdDegrees;
        BeginBusy("Analyzing source coverage...");
        try
        {
        var (coverage, gaps) = await Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); return (_azimuthAnalyzer.DetectCoverage(request, threshold), _azimuthAnalyzer.DetectGaps(request, threshold)); }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        CoverageIntervals.Clear();
        foreach (var interval in coverage)
        {
            CoverageIntervals.Add(interval);
        }

        GapIntervals.Clear();
        foreach (var gap in gaps)
        {
            GapIntervals.Add(gap);
        }

        StatusMessage = $"Source has {request.Rays.Count} rays. Detected {coverage.Count} coverage interval(s) and {gaps.Count} gap(s).";
        CompletionSummary = "Coverage analysis complete.";
        _applicationLogService?.LogInfo(StatusMessage, nameof(SourceCompletionWorkspaceViewModel));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { StatusMessage = $"Coverage analysis failed: {ex.Message}"; _applicationLogService?.LogError(StatusMessage, ex, nameof(SourceCompletionWorkspaceViewModel)); }
        finally { EndBusy(); }
    }

    private async Task GenerateCompletedSourceAsync()
    {
        var cancellationToken = (System.Windows.Application.Current as App)?.Lifetime.Token ?? CancellationToken.None;
        if (cancellationToken.IsCancellationRequested) return;
        if (SelectedProjectedSource is null || SelectedProjectedSource.Rays.Count == 0 || AngularStepDegrees <= 0d || GapThresholdDegrees <= 0d)
        {
            StatusMessage = SelectedProjectedSource is null
                ? "Select a projected light source first."
                : SelectedProjectedSource.Rays.Count == 0
                    ? "The selected projected light source has zero rays. Choose another source."
                    : AngularStepDegrees <= 0d
                        ? "Angular step must be greater than 0 degrees."
                        : "Gap threshold must be greater than 0 degrees.";
            return;
        }

        int? maxSynthetic = null;
        if (!string.IsNullOrWhiteSpace(MaxSyntheticRaysText))
        {
            if (!int.TryParse(MaxSyntheticRaysText, out var parsed) || parsed < 0)
            {
                StatusMessage = "Max synthetic rays must be empty or a non-negative integer.";
                return;
            }

            maxSynthetic = parsed;
        }

        var source = SelectedProjectedSource;
        var method = SelectedCompletionMethod;
        var request = BuildRequest(source);
        var settings = new SourceCompletionSettings(AngularStepDegrees, GapThresholdDegrees, IncludeOriginalRays, maxSynthetic, method, MirrorAxisDegrees);
        BeginBusy("Generating synthetic rays...");
        try
        {
        LastCompletionResult = await Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); return _completionService.Complete(request, settings); }, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var synthetic = LastCompletionResult.Rays.Skip(Math.Min(LastCompletionResult.OriginalRayCount, LastCompletionResult.Rays.Count)).ToList();
        var item = new CompletedSourceItem
        {
            Name = $"Completed Source {CompletedSources.Count + 1} - {method}",
            Methodology = method.ToString(),
            OriginalSourceName = source.Name,
            ProfileDefinition = source.ProfileDefinition,
            SourceFrame = source.SourceFrame,
            OriginalRays = request.Rays.ToList(),
            SyntheticRays = synthetic,
            CompletedRays = LastCompletionResult.Rays.ToList(),
            Settings = settings,
        };
        CompletedSources.Add(item);
        SelectedCompletedSource = item;

        CoverageIntervals.Clear();
        foreach (var interval in LastCompletionResult.CoverageIntervals)
        {
            CoverageIntervals.Add(interval);
        }

        GapIntervals.Clear();
        foreach (var gap in LastCompletionResult.GapIntervals)
        {
            GapIntervals.Add(gap);
        }

        CompletionSummary = $"Generated completed source using {method}. Original rays: {LastCompletionResult.OriginalRayCount}; Synthetic rays: {LastCompletionResult.SyntheticRayCount}; Output rays: {LastCompletionResult.Rays.Count}.";
        StatusMessage = "Completed source generated. Review and add to a collision scene when ready.";
        RefreshPreview();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { StatusMessage = $"Source completion failed: {ex.Message}"; _applicationLogService?.LogError(StatusMessage, ex, nameof(SourceCompletionWorkspaceViewModel)); }
        finally { EndBusy(); }
    }

    private async Task AddCompletedSourceToCollisionAsync()
    {
        if (SelectedCompletedSource is null || SelectedTargetCollisionScene is null || SelectedTargetCollisionScene.IsProjectionOnly)
        {
            StatusMessage = SelectedCompletedSource is null
                ? "Generate a completion result first, then add it to a collision scene."
                : SelectedTargetCollisionScene is null
                    ? "Select a target collision scene first."
                    : "Target must be a collision scene.";
            return;
        }

        var selected = SelectedCompletedSource;
        var target = SelectedTargetCollisionScene;
        BeginBusy($"Preparing completed source ({selected.CompletedRays.Count} rays)...");
        try
        {
        var rays = await Task.Run(() => selected.CompletedRays.ToList());
        ProgressMessage = "Adding source to collision scene...";
        var completedName = selected.Name;

        var completedSource = new ProjectedLightSourceItemViewModel
        {
            Name = completedName,
            ProfileDefinition = selected.ProfileDefinition,
            SourceFrame = selected.SourceFrame,
            BaseOrientation = BuildOrientationFromFrame(selected.SourceFrame),
            OriginKind = ProjectedLightSourceOriginKind.CompletedProjectionResult,
        };

        foreach (var ray in rays) completedSource.ExactRays.Add(ray.Ray);

        target.InvalidateCollisionResults();
        target.ProjectedLightSources.Add(completedSource);
        target.SelectedProjectedLightSource = completedSource;
        if (!ReferenceEquals(_sceneCollectionService.SelectedScene, target)) _sceneCollectionService.SelectedScene = target;
        _sceneCollectionService.NotifySceneContentChanged();

        StatusMessage = $"Added completed source '{completedSource.Name}' to collision scene '{target.Name}' with {completedSource.EffectiveRayCount} rays.";
        _applicationLogService?.LogSuccess(StatusMessage, nameof(SourceCompletionWorkspaceViewModel));
        }
        catch (Exception ex) { StatusMessage = $"Could not add completed source: {ex.Message}"; _applicationLogService?.LogError(StatusMessage, ex, nameof(SourceCompletionWorkspaceViewModel)); }
        finally { EndBusy(); }
    }
    private void RemoveSelectedCompletedSource()
    {
        if (SelectedCompletedSource is null) return;
        var idx = CompletedSources.IndexOf(SelectedCompletedSource);
        CompletedSources.Remove(SelectedCompletedSource);
        SelectedCompletedSource = CompletedSources.Count == 0 ? null : CompletedSources[Math.Min(idx, CompletedSources.Count - 1)];
    }

    private bool CanAnalyzeOrGenerate() => !IsBusy && SelectedProjectedSource is not null && SelectedProjectedSource.Rays.Count > 0;
    private bool CanAddCompletedSource() => !IsBusy && SelectedCompletedSource is not null && SelectedTargetCollisionScene is not null && !SelectedTargetCollisionScene.IsProjectionOnly;

    public void AttachViewport(HelixToolkit.Wpf.HelixViewport3D viewport)
    {
        _previewRenderSyncService = new SourceCompletionPreviewRenderSyncService(viewport);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_previewRenderSyncService is null)
        {
            return;
        }

        if (SelectedCompletedSource is not null)
        {
            _previewRenderSyncService.SyncCompletedSourcePreview(SelectedCompletedSource);
            return;
        }

        _previewRenderSyncService.SyncPreview(SelectedProjectedSource, LastCompletionResult);
    }

    private void RaiseCommandStates()
    {
        (AnalyzeCoverageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (GenerateCompletedSourceCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddCompletedSourceToCollisionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSelectedCompletedSourceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RefreshSourcesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void PopulateProjectionResultInputs()
    {
        if (_projectionWorkspace?.SavedResults is null)
        {
            return;
        }

        foreach (var result in _projectionWorkspace.SavedResults)
        {
            try
            {
                var source = _projectionResultToCollisionSourceService.CreateProjectedLightSource(result);
                AvailableProjectedSources.Add(new SourceCompletionInputItem { Identity = new ProjectedSourceIdentity(_projectionWorkspace.SelectedScene?.Name ?? string.Empty, result.Key), Name = source.Name, Source = source, OriginText = "Projection Result" });
            }
            catch (InvalidOperationException)
            {
                // Ignore projection results that do not have complete axisymmetric profile/source data.
            }
        }
    }

    private void InvalidateTransientState()
    {
        LastCompletionResult = null;
        CoverageIntervals.Clear();
        GapIntervals.Clear();
        CompletionSummary = "No completed source generated yet.";
        SelectedCompletedSource = null;
    }

    private void BeginBusy(string message) { ProgressMessage = message; IsBusy = true; RaisePropertyChanged(nameof(IsProgressVisible)); }
    private void EndBusy() { IsBusy = false; ProgressMessage = string.Empty; RaisePropertyChanged(nameof(IsProgressVisible)); }

    private static System.Numerics.Quaternion BuildOrientationFromFrame(LaserCollisionIn3DObjects.Domain.Projection.PointSourceFrameState frame)
    {
        var x = new System.Numerics.Vector3((float)frame.AxisX.X, (float)frame.AxisX.Y, (float)frame.AxisX.Z);
        var y = new System.Numerics.Vector3((float)frame.AxisY.X, (float)frame.AxisY.Y, (float)frame.AxisY.Z);
        var z = new System.Numerics.Vector3((float)frame.AxisZ.X, (float)frame.AxisZ.Y, (float)frame.AxisZ.Z);
        return System.Numerics.Quaternion.CreateFromRotationMatrix(new System.Numerics.Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1));
    }

}
