using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Wpf.Commands;
using LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.Services;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using System.Windows.Input;

namespace LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;

public sealed class GraphicMasterViewModel : ObservableObject
{
    private readonly SceneCollectionService _sceneCollectionService;
    private readonly GraphSourceExtractionService _sourceExtractionService = new();
    private readonly GraphTypeRegistry _graphTypeRegistry = new(new IGraphType[]
    {
        new AngleBinBarChartGraphType(),
        new AngleBinXyChartGraphType(),
        new CylindricalNormalizedAxialAngleXyGraphType(),
    });
    private readonly IGraphicMasterSaveFileDialogService _saveFileDialogService;
    private readonly IGraphicMasterPngExportService _pngExportService;

    private GraphTypeOptionViewModel? _selectedGraphType;
    private StoredGraphChartViewModel? _selectedStoredChart;
    private double _binSizeDeg = 10;
    private PlotModel _plotModel = CreateEmptyPlotModel();
    private string _statusMessage = "Select graph type and data sources, then generate chart.";
    private string _chartName = "Chart 1";

    public GraphicMasterViewModel(
        SceneCollectionService sceneCollectionService,
        IGraphicMasterSaveFileDialogService? saveFileDialogService = null,
        IGraphicMasterPngExportService? pngExportService = null)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));
        _saveFileDialogService = saveFileDialogService ?? new GraphicMasterSaveFileDialogService();
        _pngExportService = pngExportService ?? new GraphicMasterPngExportService();

        foreach (var graphType in _graphTypeRegistry.GraphTypes)
        {
            GraphTypes.Add(new GraphTypeOptionViewModel { GraphType = graphType });
        }

        SelectedGraphType = GraphTypes.FirstOrDefault();

        GenerateChartCommand = new RelayCommand(GenerateChart);
        DeleteStoredChartCommand = new RelayCommand(DeleteStoredChart, () => SelectedStoredChart is not null);
        SaveChartAsPngCommand = new RelayCommand(SaveChartAsPng);

        _sceneCollectionService.Scenes.CollectionChanged += OnScenesCollectionChanged;
        foreach (var scene in _sceneCollectionService.Scenes)
        {
            AttachSceneObservers(scene);
        }
        RefreshSources();
    }

    public ObservableCollection<GraphTypeOptionViewModel> GraphTypes { get; } = new();
    public ObservableCollection<GraphableSourceItemViewModel> Sources { get; } = new();
    public ObservableCollection<StoredGraphChartViewModel> StoredCharts { get; } = new();

    public ICommand GenerateChartCommand { get; }
    public ICommand DeleteStoredChartCommand { get; }
    public ICommand SaveChartAsPngCommand { get; }

    public GraphTypeOptionViewModel? SelectedGraphType
    {
        get => _selectedGraphType;
        set => SetProperty(ref _selectedGraphType, value);
    }

    public StoredGraphChartViewModel? SelectedStoredChart
    {
        get => _selectedStoredChart;
        set
        {
            if (!SetProperty(ref _selectedStoredChart, value))
            {
                return;
            }

            if (DeleteStoredChartCommand is RelayCommand deleteCommand)
            {
                deleteCommand.RaiseCanExecuteChanged();
            }

            if (value is not null)
            {
                RestoreStoredChart(value);
            }
        }
    }

    public string ChartName
    {
        get => _chartName;
        set => SetProperty(ref _chartName, value);
    }

    public double BinSizeDeg
    {
        get => _binSizeDeg;
        set => SetProperty(ref _binSizeDeg, value);
    }

    public PlotModel PlotModel
    {
        get => _plotModel;
        private set => SetProperty(ref _plotModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void GenerateChart()
    {
        RefreshSources();

        var selectedGraphType = SelectedGraphType?.GraphType;
        if (selectedGraphType is null)
        {
            StatusMessage = "Select a graph type.";
            return;
        }

        if (BinSizeDeg <= 0 || BinSizeDeg > 180)
        {
            StatusMessage = "Bin size must be in the range (0, 180].";
            return;
        }

        var selectedSourceIds = Sources.Where(source => source.IsSelected).Select(source => source.SourceData.Id).ToList();
        if (selectedSourceIds.Count == 0)
        {
            StatusMessage = "Select at least one source or projection result.";
            return;
        }

        var chartResult = RenderFromConfiguration(selectedGraphType.Id, BinSizeDeg, selectedSourceIds, chartNameOverride: ChartName);
        if (!chartResult.Success)
        {
            return;
        }

        var normalizedChartName = string.IsNullOrWhiteSpace(ChartName)
            ? $"{selectedGraphType.DisplayName} {StoredCharts.Count + 1}"
            : ChartName.Trim();

        var stored = new StoredGraphChartViewModel
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = normalizedChartName,
            GraphTypeId = selectedGraphType.Id,
            BinSizeDeg = BinSizeDeg,
            SelectedSourceIds = selectedSourceIds,
        };

        StoredCharts.Add(stored);
        SelectedStoredChart = stored;
        StatusMessage = $"Generated and stored '{stored.DisplayName}'.";
    }

    private void RestoreStoredChart(StoredGraphChartViewModel storedChart)
    {
        var graphType = GraphTypes.FirstOrDefault(type => type.GraphType.Id == storedChart.GraphTypeId);
        if (graphType is null)
        {
            StatusMessage = $"Stored graph type '{storedChart.GraphTypeId}' is no longer available.";
            return;
        }

        SelectedGraphType = graphType;
        ChartName = storedChart.DisplayName;
        BinSizeDeg = storedChart.BinSizeDeg;

        RefreshSources();
        var selectedIds = storedChart.SelectedSourceIds.ToHashSet(StringComparer.Ordinal);
        foreach (var source in Sources)
        {
            source.IsSelected = selectedIds.Contains(source.SourceData.Id);
        }

        _ = RenderFromConfiguration(storedChart.GraphTypeId, storedChart.BinSizeDeg, storedChart.SelectedSourceIds, chartNameOverride: storedChart.DisplayName);
    }

    private void DeleteStoredChart()
    {
        if (SelectedStoredChart is null)
        {
            StatusMessage = "Select a stored chart to delete.";
            return;
        }

        var deleted = SelectedStoredChart;
        var deletedIndex = StoredCharts.IndexOf(deleted);
        StoredCharts.Remove(deleted);

        if (StoredCharts.Count == 0)
        {
            SelectedStoredChart = null;
            PlotModel = CreateEmptyPlotModel();
            StatusMessage = $"Deleted '{deleted.DisplayName}'. No stored charts remain.";
            return;
        }

        var nextIndex = Math.Clamp(deletedIndex, 0, StoredCharts.Count - 1);
        SelectedStoredChart = StoredCharts[nextIndex];
        StatusMessage = $"Deleted '{deleted.DisplayName}'.";
    }

    private void SaveChartAsPng()
    {
        if (PlotModel.Series.Count == 0)
        {
            StatusMessage = "No chart is currently available to save.";
            return;
        }

        var path = _saveFileDialogService.SelectPngPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Save canceled.";
            return;
        }

        _pngExportService.Export(PlotModel, path, width: 1600, height: 1000);
        StatusMessage = $"Saved chart to '{path}'.";
    }

    private (bool Success, int SourceCount) RenderFromConfiguration(string graphTypeId, double binSizeDeg, IReadOnlyList<string> sourceIds, string? chartNameOverride = null)
    {
        var selectedGraphType = _graphTypeRegistry.Resolve(graphTypeId);
        var selectedSourceSet = sourceIds.ToHashSet(StringComparer.Ordinal);
        var selectedSources = Sources
            .Where(source => selectedSourceSet.Contains(source.SourceData.Id))
            .Select(source => source.SourceData)
            .ToList();

        var result = selectedGraphType.Build(new GraphBuildContext
        {
            Sources = selectedSources,
            BinSizeDeg = binSizeDeg,
        });

        if (result.Series.Count == 0)
        {
            PlotModel = CreateEmptyPlotModel();
            StatusMessage = selectedGraphType is CylindricalNormalizedAxialAngleXyGraphType
                ? "No compatible cylindrical sources selected for normalized axial-angle XY graph."
                : "No chart data was produced for the selected sources.";
            return (false, 0);
        }

        PlotModel = BuildPlotModel(result, chartNameOverride ?? selectedGraphType.DisplayName);
        return (true, selectedSources.Count);
    }

    private static PlotModel BuildPlotModel(GraphResult result, string title)
    {
        var plotModel = new PlotModel { Title = title };

        if (result.Series.Count == 0)
        {
            return plotModel;
        }

        if (result.VisualizationKind == GraphVisualizationKind.GroupedBar)
        {
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Angle (deg)", Minimum = 0, Maximum = 180 });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Ray Count", Minimum = 0 });

            var seriesCount = result.Series.Count;
            var binTemplate = result.Series[0].Bins;

            for (var seriesIndex = 0; seriesIndex < result.Series.Count; seriesIndex++)
            {
                var sourceSeries = result.Series[seriesIndex];
                var rectangleSeries = new RectangleBarSeries { Title = sourceSeries.Name, StrokeThickness = 1 };
                for (var binIndex = 0; binIndex < sourceSeries.Bins.Count; binIndex++)
                {
                    var templateBin = binTemplate[binIndex];
                    var sourceBin = sourceSeries.Bins[binIndex];
                    var totalWidth = templateBin.BinEndDeg - templateBin.BinStartInclusiveDeg;
                    var barWidth = totalWidth / seriesCount;
                    var x0 = templateBin.BinStartInclusiveDeg + (seriesIndex * barWidth);
                    var x1 = x0 + barWidth;
                    rectangleSeries.Items.Add(new RectangleBarItem(x0, 0, x1, sourceBin.Count));
                }

                plotModel.Series.Add(rectangleSeries);
            }

            return plotModel;
        }

        var usesScatter = result.Series.Any(series => series.Points.Count > 0);

        if (usesScatter)
        {
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Normalized axial position (x/L)" });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Angle to source X axis (deg)", Minimum = 0, Maximum = 180 });

            foreach (var series in result.Series)
            {
                var scatter = new ScatterSeries { Title = series.Name, MarkerType = MarkerType.Circle, MarkerSize = 2.5 };
                foreach (var point in series.Points)
                {
                    scatter.Points.Add(new ScatterPoint(point.X, point.Y));
                }

                plotModel.Series.Add(scatter);
            }

            return plotModel;
        }

        plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Angle Bin Center (deg)", Minimum = 0, Maximum = 180 });
        plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Ray Count", Minimum = 0 });

        foreach (var series in result.Series)
        {
            var lineSeries = new LineSeries { Title = series.Name, StrokeThickness = 2, MarkerType = MarkerType.Circle, MarkerSize = 3 };
            foreach (var bin in series.Bins)
            {
                lineSeries.Points.Add(new DataPoint(bin.BinCenterDeg, bin.Count));
            }

            plotModel.Series.Add(lineSeries);
        }

        return plotModel;
    }

    private void RefreshSources()
    {
        var selectedIds = Sources.Where(source => source.IsSelected).Select(source => source.SourceData.Id).ToHashSet(StringComparer.Ordinal);

        var scenes = _sceneCollectionService.Scenes
            .Select(scene => new GraphSceneData
            {
                SceneName = scene.Name,
                CylindricalSources = scene.LightSources.Select(MapToDomainLightSource).ToList(),
                ProjectionResults = scene.ProjectionState.SavedResults,
            })
            .ToList();

        var extracted = _sourceExtractionService.Extract(scenes);

        Sources.Clear();
        foreach (var source in extracted)
        {
            Sources.Add(new GraphableSourceItemViewModel
            {
                SourceData = source,
                IsSelected = selectedIds.Contains(source.Id),
            });
        }

        if (Sources.Count == 0)
        {
            PlotModel = CreateEmptyPlotModel();
            StatusMessage = "No graphable sources were found. Add cylindrical light sources or projection results.";
        }
    }

    private static CylindricalLightSource MapToDomainLightSource(CylindricalLightSourceItemViewModel source)
    {
        var orientation = FrameOrientationBuilder.ApplyLocalEulerDegrees(
            source.BaseOrientation,
            source.RotationX,
            source.RotationY,
            source.RotationZ);

        return new CylindricalLightSource(
            string.IsNullOrWhiteSpace(source.Name) ? "Light Source" : source.Name,
            new Frame3D(new Vector3(source.PositionX, source.PositionY, source.PositionZ), orientation),
            source.Radius,
            source.Height,
            source.RayCount,
            source.TiltWeight);
    }

    private void OnScenesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var removedScene in e.OldItems.OfType<CollisionSceneViewModel>())
            {
                DetachSceneObservers(removedScene);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var addedScene in e.NewItems.OfType<CollisionSceneViewModel>())
            {
                AttachSceneObservers(addedScene);
            }
        }

        RefreshSources();
    }

    private void AttachSceneObservers(CollisionSceneViewModel scene)
    {
        scene.LightSources.CollectionChanged += OnSceneGraphInputsChanged;
        scene.ProjectionState.SavedResults.CollectionChanged += OnSceneGraphInputsChanged;
    }

    private void DetachSceneObservers(CollisionSceneViewModel scene)
    {
        scene.LightSources.CollectionChanged -= OnSceneGraphInputsChanged;
        scene.ProjectionState.SavedResults.CollectionChanged -= OnSceneGraphInputsChanged;
    }

    private void OnSceneGraphInputsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshSources();

    private static PlotModel CreateEmptyPlotModel()
    {
        return new PlotModel { Title = "Graphic Master" };
    }
}
