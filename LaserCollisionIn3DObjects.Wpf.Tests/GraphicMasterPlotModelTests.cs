using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Domain.Persistence;
using LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;
using LaserCollisionIn3DObjects.Wpf.Services;
using OxyPlot.Axes;
using OxyPlot.Legends;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class GraphicMasterPlotModelTests
{
    [Fact]
    public void SimilarityCommandBuildsSymmetricMatrixAndMetricSwitchReusesPairResults()
    {
        var viewModel = SimilarityViewModel(3);
        foreach (var source in viewModel.Sources) source.IsSelected = true;

        Assert.True(viewModel.CompareSelectedSourcesCommand.CanExecute(null));
        viewModel.CompareSelectedSourcesCommand.Execute(null);

        Assert.Equal(3, viewModel.SimilarityPairs.Count);
        Assert.True(viewModel.ShowsSimilarityMatrix);
        var matrix = viewModel.SimilarityMatrix!;
        Assert.Equal("1.0000", matrix[0][1]);
        Assert.Equal(matrix[0][2], matrix[1][1]);
        var storedPair = viewModel.SimilarityPairs[0].Result;

        viewModel.SelectedSimilarityMetric = viewModel.SimilarityMetrics.Single(option => option.Metric == SimilarityMetric.HistogramIntersection);

        Assert.Same(storedPair, viewModel.SimilarityPairs[0].Result);
        Assert.Equal(matrix.Count, viewModel.SimilarityMatrix!.Count);
    }

    [Fact]
    public void BinSizeAndSelectionChangesClearSimilarityResults()
    {
        var viewModel = SimilarityViewModel(2);
        foreach (var source in viewModel.Sources) source.IsSelected = true;
        viewModel.CompareSelectedSourcesCommand.Execute(null);
        Assert.True(viewModel.HasSimilarityResults);

        viewModel.AngleBinSizeDeg = 20;
        Assert.False(viewModel.HasSimilarityResults);

        viewModel.CompareSelectedSourcesCommand.Execute(null);
        viewModel.Sources[0].IsSelected = false;
        Assert.False(viewModel.HasSimilarityResults);
        Assert.False(viewModel.CompareSelectedSourcesCommand.CanExecute(null));
    }

    [Fact]
    public void TwoSeriesLineGraphCreatesOutsideSourceLegend()
    {
        var model = GraphicMasterViewModel.BuildPlotModel(LineResult("Source A", "Source B"), "Line chart");

        Assert.Equal(2, model.Series.Count);
        var legend = Assert.Single(model.Legends);
        Assert.True(model.IsLegendVisible);
        Assert.True(legend.IsLegendVisible);
        Assert.Equal(LegendPlacement.Outside, legend.LegendPlacement);
        Assert.Equal(LegendPosition.RightTop, legend.LegendPosition);
        Assert.Equal(LegendOrientation.Vertical, legend.LegendOrientation);
        Assert.Equal(["Source A", "Source B"], model.Series.Select(series => series.Title));
    }

    [Fact]
    public void ThreeSeriesGroupedBarGraphPreservesSourceTitlesInOneLegend()
    {
        var result = new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.AngleGroupedBar,
            Series = [BinSeries("Source A"), BinSeries("Source B"), BinSeries("Source C")],
        };

        var model = GraphicMasterViewModel.BuildPlotModel(result, "Bars");

        Assert.Equal(3, model.Series.Count);
        Assert.Single(model.Legends);
        Assert.True(model.IsLegendVisible);
        Assert.Equal(["Source A", "Source B", "Source C"], model.Series.Select(series => series.Title));
    }

    [Fact]
    public void AzimuthRayCountGraphCreatesLegendForMultipleSources()
    {
        var result = new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.AzimuthGroupedBar,
            Series = [BinSeries("Source A"), BinSeries("Source B")],
        };

        var model = GraphicMasterViewModel.BuildPlotModel(result, "Azimuth");

        Assert.Equal(2, model.Series.Count);
        Assert.Single(model.Legends);
        Assert.True(model.IsLegendVisible);
        Assert.Equal(["Source A", "Source B"], model.Series.Select(series => series.Title));
    }

    [Fact]
    public void SingleSeriesGraphDoesNotCreateLegend()
    {
        var model = GraphicMasterViewModel.BuildPlotModel(LineResult("Only Source"), "Line chart");

        Assert.Single(model.Series);
        Assert.Empty(model.Legends);
        Assert.False(model.IsLegendVisible);
    }

    [Fact]
    public void NormalizedAxialAngleGraphCreatesLegendForMultipleSources()
    {
        var result = new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.NormalizedAxialAngleXyLine,
            Series = [PointSeries("Cylinder A"), PointSeries("Cylinder B")],
        };

        var model = GraphicMasterViewModel.BuildPlotModel(result, "Normalized");

        Assert.Equal(2, model.Series.Count);
        Assert.Single(model.Legends);
        Assert.True(model.IsLegendVisible);
        Assert.Equal(["Cylinder A", "Cylinder B"], model.Series.Select(series => series.Title));
    }

    [Fact]
    public void HeatmapKeepsRayCountColorAxisWithoutSourceLegend()
    {
        var result = new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.AzimuthPolarHeatmap,
            Series = [],
            Heatmap = new HeatmapGridData
            {
                Name = "Source A",
                XMin = 0,
                XMax = 360,
                YMin = 0,
                YMax = 180,
                Values = new double[,] { { 1 } },
            },
        };

        var model = GraphicMasterViewModel.BuildPlotModel(result, "Heatmap");

        Assert.Empty(model.Legends);
        Assert.False(model.IsLegendVisible);
        Assert.Equal("Ray Count", Assert.Single(model.Axes.OfType<LinearColorAxis>()).Title);
    }

    [Fact]
    public void RestoredStoredMultiSourceChartRebuildsLegend()
    {
        var viewModel = new GraphicMasterViewModel(new SceneCollectionService(), new CompletedSourceStore());
        var state = new GraphicMasterState
        {
            ImportedSources =
            [
                new ImportedGraphSourceState { Id = "source-a", Source = TransferSource("Source A") },
                new ImportedGraphSourceState { Id = "source-b", Source = TransferSource("Source B") },
            ],
            StoredCharts =
            [
                new StoredGraphChartState
                {
                    Id = "stored-chart",
                    DisplayName = "Stored sources",
                    GraphTypeId = "graph.angle-bin-xy",
                    AngleBinSizeDeg = 10,
                    AzimuthBinSizeDeg = 15,
                    PolarBinSizeDeg = 10,
                    SelectedSourceIds = ["source-a", "source-b"],
                },
            ],
            SelectedChartId = "stored-chart",
        };

        viewModel.ApplyState(state);

        Assert.Equal(2, viewModel.PlotModel.Series.Count);
        Assert.Single(viewModel.PlotModel.Legends);
        Assert.True(viewModel.PlotModel.IsLegendVisible);
        Assert.Equal(["Imported / Source A", "Imported / Source B"], viewModel.PlotModel.Series.Select(series => series.Title));
    }

    private static GraphResult LineResult(params string[] names) => new()
    {
        VisualizationKind = GraphVisualizationKind.AngleBinXyLine,
        Series = names.Select(BinSeries).ToList(),
    };

    private static GraphSeriesData BinSeries(string name) => new()
    {
        Name = name,
        Bins = [new AngleBinCount(0, 10, 5, 1)],
    };

    private static GraphSeriesData PointSeries(string name) => new()
    {
        Name = name,
        Points = [new ScatterPointData(0.5, 45)],
    };

    private static LightSourceTransferData TransferSource(string name) => new(
        name,
        new AxisymmetricSourceProfileDefinition { Radius = 1, Length = 2 },
        new Frame3D(Vector3.Zero, Quaternion.Identity),
        0,
        Vector3.Zero,
        [new LightSourceTransferRay(Vector3.Zero, Vector3.UnitX)]);

    private static GraphicMasterViewModel SimilarityViewModel(int sourceCount)
    {
        var viewModel = new GraphicMasterViewModel(new SceneCollectionService(), new CompletedSourceStore());
        viewModel.ApplyState(new GraphicMasterState
        {
            ImportedSources = Enumerable.Range(0, sourceCount).Select(index => new ImportedGraphSourceState
            {
                Id = $"source-{index}",
                Source = TransferSource($"Source {index}"),
            }).ToList(),
        });
        return viewModel;
    }
}
