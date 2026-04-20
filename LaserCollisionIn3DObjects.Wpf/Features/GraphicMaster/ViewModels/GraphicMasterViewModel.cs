using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Wpf.Commands;
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
    });

    private GraphTypeOptionViewModel? _selectedGraphType;
    private double _binSizeDeg = 10;
    private PlotModel _plotModel = CreateEmptyPlotModel();
    private string _statusMessage = "Select graph type and data sources, then generate chart.";

    public GraphicMasterViewModel(SceneCollectionService sceneCollectionService)
    {
        _sceneCollectionService = sceneCollectionService ?? throw new ArgumentNullException(nameof(sceneCollectionService));

        foreach (var graphType in _graphTypeRegistry.GraphTypes)
        {
            GraphTypes.Add(new GraphTypeOptionViewModel { GraphType = graphType });
        }

        SelectedGraphType = GraphTypes.FirstOrDefault();

        GenerateChartCommand = new RelayCommand(GenerateChart);

        _sceneCollectionService.Scenes.CollectionChanged += OnScenesCollectionChanged;
        RefreshSources();
    }

    public ObservableCollection<GraphTypeOptionViewModel> GraphTypes { get; } = new();
    public ObservableCollection<GraphableSourceItemViewModel> Sources { get; } = new();

    public ICommand GenerateChartCommand { get; }

    public GraphTypeOptionViewModel? SelectedGraphType
    {
        get => _selectedGraphType;
        set => SetProperty(ref _selectedGraphType, value);
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

        var selectedSources = Sources.Where(source => source.IsSelected).Select(source => source.SourceData).ToList();
        if (selectedSources.Count == 0)
        {
            StatusMessage = "Select at least one source or projection result.";
            return;
        }

        var result = selectedGraphType.Build(new GraphBuildContext
        {
            Sources = selectedSources,
            BinSizeDeg = BinSizeDeg,
        });

        PlotModel = BuildPlotModel(result);
        StatusMessage = $"Generated {selectedGraphType.DisplayName} for {selectedSources.Count} source(s).";
    }

    private PlotModel BuildPlotModel(GraphResult result)
    {
        var plotModel = new PlotModel
        {
            Title = SelectedGraphType?.DisplayName ?? "Graph",
        };

        if (result.Series.Count == 0)
        {
            return plotModel;
        }

        if (result.VisualizationKind == GraphVisualizationKind.GroupedBar)
        {
            var categoryAxis = new CategoryAxis { Position = AxisPosition.Left, Title = "Angle Bin (deg)" };
            var valueAxis = new LinearAxis { Position = AxisPosition.Bottom, Title = "Ray Count", Minimum = 0 };

            foreach (var bin in result.Series[0].Bins)
            {
                var end = Math.Abs(bin.BinEndDeg - 180d) < 0.0001 ? "]" : ")";
                categoryAxis.Labels.Add($"[{bin.BinStartInclusiveDeg:0.#},{bin.BinEndDeg:0.#}{end}");
            }

            plotModel.Axes.Add(categoryAxis);
            plotModel.Axes.Add(valueAxis);

            foreach (var series in result.Series)
            {
                var columnSeries = new BarSeries { Title = series.Name, StrokeThickness = 1 };
                foreach (var bin in series.Bins)
                {
                    columnSeries.Items.Add(new BarItem(bin.Count));
                }

                plotModel.Series.Add(columnSeries);
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
            source.RayCount);
    }

    private void OnScenesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshSources();

    private static PlotModel CreateEmptyPlotModel()
    {
        return new PlotModel { Title = "Graphic Master" };
    }
}
