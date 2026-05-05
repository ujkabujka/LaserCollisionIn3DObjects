using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using LaserCollisionIn3DObjects.Domain.SourceCompletion;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.SourceCompletion.ViewModels;

public sealed class SourceCompletionWorkspaceViewModel : ObservableObject
{
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly ApplicationLogService? _applicationLogService;
    private readonly ProjectedSourceAzimuthAnalyzer _azimuthAnalyzer = new();
    private readonly ProjectedSourceCompletionService _completionService = new();
    private ProjectedLightSourceItemViewModel? _selectedProjectedSource;
    private CollisionSceneViewModel? _selectedTargetCollisionScene;
    private double _angularStepDegrees = 5d;
    private double _gapThresholdDegrees = 10d;
    private bool _includeOriginalRays = true;
    private string _maxSyntheticRaysText = string.Empty;
    private SourceCompletionMethod _selectedCompletionMethod = SourceCompletionMethod.RotationalCopy;
    private double _mirrorAxisDegrees;
    private string _weightedSectorsText = string.Empty;
    private ProjectedSourceCompletionResult? _lastCompletionResult;
    private string _statusMessage = "Select a projected source to analyze.";
    private string _completionSummary = "No completed source generated yet.";

    public SourceCompletionWorkspaceViewModel(SceneCollectionService sceneCollectionService, ApplicationLogService? applicationLogService = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _applicationLogService = applicationLogService;

        RefreshSourcesCommand = new RelayCommand(RefreshSources);
        AnalyzeCoverageCommand = new RelayCommand(AnalyzeCoverage, CanAnalyzeOrGenerate);
        GenerateCompletedSourceCommand = new RelayCommand(GenerateCompletedSource, CanAnalyzeOrGenerate);
        AddCompletedSourceToCollisionCommand = new RelayCommand(AddCompletedSourceToCollision, CanAddCompletedSource);

        _sceneCollectionService.Scenes.CollectionChanged += (_, _) => RefreshSources();
        _sceneCollectionService.SceneContentChanged += (_, _) => RefreshSources();
        RefreshSources();
    }

    public ObservableCollection<ProjectedLightSourceItemViewModel> AvailableProjectedSources { get; } = new();
    public ObservableCollection<CollisionSceneViewModel> TargetCollisionScenes { get; } = new();
    public ObservableCollection<AzimuthCoverageInterval> CoverageIntervals { get; } = new();
    public ObservableCollection<AzimuthGapInterval> GapIntervals { get; } = new();
    public ObservableCollection<SourceCompletionMethod> CompletionMethods { get; } = new(Enum.GetValues<SourceCompletionMethod>());

    public ProjectedLightSourceItemViewModel? SelectedProjectedSource
    {
        get => _selectedProjectedSource;
        set
        {
            if (SetProperty(ref _selectedProjectedSource, value))
            {
                RaiseCommandStates();
            }
        }
    }

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
    public string WeightedSectorsText { get => _weightedSectorsText; set => SetProperty(ref _weightedSectorsText, value); }

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

    public ICommand RefreshSourcesCommand { get; }
    public ICommand AnalyzeCoverageCommand { get; }
    public ICommand GenerateCompletedSourceCommand { get; }
    public ICommand AddCompletedSourceToCollisionCommand { get; }

    private void RefreshSources()
    {
        AvailableProjectedSources.Clear();
        TargetCollisionScenes.Clear();

        foreach (var scene in _sceneCollectionService.Scenes)
        {
            if (!scene.IsProjectionOnly)
            {
                TargetCollisionScenes.Add(scene);
            }

            foreach (var projectedSource in scene.ProjectedLightSources)
            {
                AvailableProjectedSources.Add(projectedSource);
            }
        }

        if (SelectedProjectedSource is null || !AvailableProjectedSources.Contains(SelectedProjectedSource))
        {
            SelectedProjectedSource = AvailableProjectedSources.FirstOrDefault();
        }

        if (SelectedTargetCollisionScene is null || !TargetCollisionScenes.Contains(SelectedTargetCollisionScene))
        {
            SelectedTargetCollisionScene = TargetCollisionScenes.FirstOrDefault();
        }

        RaiseCommandStates();
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

    private void AnalyzeCoverage()
    {
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
        var coverage = _azimuthAnalyzer.DetectCoverage(request, GapThresholdDegrees);
        var gaps = _azimuthAnalyzer.DetectGaps(request, GapThresholdDegrees);

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

    private void GenerateCompletedSource()
    {
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

        var request = BuildRequest(SelectedProjectedSource);
        var weightedSectors = ParseWeightedSectorsOrNull();
        if (SelectedCompletionMethod == SourceCompletionMethod.WeightedSectorClone && (weightedSectors is null || weightedSectors.Count == 0))
        {
            StatusMessage = "Weighted sector format must be like 60-90:2;210-240:1 and contain at least one matching source sector.";
            return;
        }

        var settings = new SourceCompletionSettings(AngularStepDegrees, GapThresholdDegrees, IncludeOriginalRays, maxSynthetic, SelectedCompletionMethod, MirrorAxisDegrees, weightedSectors);
        LastCompletionResult = _completionService.Complete(request, settings);

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

        CompletionSummary = $"Generated completed source using {SelectedCompletionMethod}. Original rays: {LastCompletionResult.OriginalRayCount}; Synthetic rays: {LastCompletionResult.SyntheticRayCount}; Output rays: {LastCompletionResult.Rays.Count}.";
        StatusMessage = "Completed source generated. Review and add to a collision scene when ready.";
    }

    private void AddCompletedSourceToCollision()
    {
        if (LastCompletionResult is null || SelectedProjectedSource is null || SelectedTargetCollisionScene is null || SelectedTargetCollisionScene.IsProjectionOnly)
        {
            StatusMessage = LastCompletionResult is null
                ? "Generate a completion result first, then add it to a collision scene."
                : SelectedTargetCollisionScene is null
                    ? "Select a target collision scene first."
                    : "Target must be a collision scene.";
            return;
        }

        var completedName = LastCompletionResult.Name.StartsWith("Completed - ", StringComparison.Ordinal)
            ? LastCompletionResult.Name
            : $"Completed - {SelectedProjectedSource.Name}";

        var completedSource = new ProjectedLightSourceItemViewModel
        {
            Name = completedName,
            ProfileDefinition = SelectedProjectedSource.ProfileDefinition,
            SourceFrame = SelectedProjectedSource.SourceFrame,
            BaseOrientation = SelectedProjectedSource.BaseOrientation,
            OriginKind = ProjectedLightSourceOriginKind.CompletedProjectionResult,
        };

        foreach (var ray in LastCompletionResult.Rays)
        {
            completedSource.Rays.Add(ray);
        }

        SelectedTargetCollisionScene.ProjectedLightSources.Add(completedSource);
        SelectedTargetCollisionScene.SelectedProjectedLightSource = completedSource;
        _sceneCollectionService.SelectedScene = SelectedTargetCollisionScene;
        _sceneCollectionService.NotifySceneContentChanged();

        StatusMessage = $"Added completed source '{completedSource.Name}' to collision scene '{SelectedTargetCollisionScene.Name}' with {completedSource.Rays.Count} rays.";
        _applicationLogService?.LogSuccess(StatusMessage, nameof(SourceCompletionWorkspaceViewModel));
    }

    private bool CanAnalyzeOrGenerate() => SelectedProjectedSource is not null && SelectedProjectedSource.Rays.Count > 0;
    private bool CanAddCompletedSource() => LastCompletionResult is not null && SelectedTargetCollisionScene is not null;

    private List<WeightedSourceSector>? ParseWeightedSectorsOrNull()
    {
        if (string.IsNullOrWhiteSpace(WeightedSectorsText))
        {
            return null;
        }

        var sectors = new List<WeightedSourceSector>();
        var tokens = WeightedSectorsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var parts = token.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                StatusMessage = $"Invalid weighted sector '{token}'. Expected start-end:weight.";
                return null;
            }

            var range = parts[0].Split('-', StringSplitOptions.TrimEntries);
            if (range.Length != 2
                || !double.TryParse(range[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
                || !double.TryParse(range[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
                || weight <= 0d)
            {
                StatusMessage = $"Invalid weighted sector '{token}'. Expected start-end:weight with positive weight.";
                return null;
            }

            sectors.Add(new WeightedSourceSector(start, end, weight));
        }

        return sectors;
    }

    private void RaiseCommandStates()
    {
        (AnalyzeCoverageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GenerateCompletedSourceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddCompletedSourceToCollisionCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
