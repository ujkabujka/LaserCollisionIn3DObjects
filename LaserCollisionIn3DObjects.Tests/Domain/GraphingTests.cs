using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Domain.Projection;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class GraphingTests
{
    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(90, 0, 1, 0)]
    [InlineData(180, -1, 0, 0)]
    [InlineData(270, 0, -1, 0)]
    public void Azimuth_UsesLocalYZBasis(double expected, float y, float z, float x)
    {
        var azimuth = RayAngleMath.CalculateAzimuthDegrees(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(x, y, z));
        Assert.Equal(expected, azimuth, 5);
        Assert.InRange(azimuth, 0, 360);
    }

    [Fact]
    public void PolarAngle_UsesLocalXAxis()
    {
        Assert.Equal(0, RayAngleMath.CalculatePolarDegrees(Vector3.UnitX, Vector3.UnitX), 5);
        Assert.Equal(90, RayAngleMath.CalculatePolarDegrees(Vector3.UnitX, Vector3.UnitY), 5);
        Assert.Equal(180, RayAngleMath.CalculatePolarDegrees(Vector3.UnitX, -Vector3.UnitX), 5);
    }

    [Fact]
    public void AzimuthHistogram_PlacesCountsIntoBins()
    {
        var histogram = new AzimuthHistogramService();
        var bins = histogram.CreateHistogram(
            [
                new Ray3D(Vector3.Zero, Vector3.UnitY),
                new Ray3D(Vector3.Zero, Vector3.UnitZ),
                new Ray3D(Vector3.Zero, -Vector3.UnitY),
                new Ray3D(Vector3.Zero, -Vector3.UnitZ),
            ],
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ,
            90);

        Assert.Equal(4, bins.Count);
        Assert.All(bins, bin => Assert.Equal(1, bin.Count));
    }

    [Fact]
    public void Heatmap_PlacesCountsIntoAzimuthPolarCells()
    {
        var service = new AzimuthPolarHeatmapService();
        var heatmap = service.Create(
            "h",
            [
                new Ray3D(Vector3.Zero, Vector3.UnitY),
                new Ray3D(Vector3.Zero, Vector3.UnitZ),
            ],
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ,
            90,
            90);

        Assert.Equal(4, heatmap.Values.GetLength(0));
        Assert.Equal(2, heatmap.Values.GetLength(1));
        Assert.Equal(1, heatmap.Values[0, 1]);
        Assert.Equal(1, heatmap.Values[1, 1]);
    }

    [Fact]
    public void AzimuthPolarHeatmapGraphType_ReturnsHeatmapResult()
    {
        var graphType = new AzimuthPolarHeatmapGraphType();
        var result = graphType.Build(new GraphBuildContext
        {
            AzimuthBinSizeDeg = 90,
            PolarBinSizeDeg = 90,
            Sources =
            [
                BuildSource("s1", GraphableSourceKind.CylindricalLightSource, [new Ray3D(Vector3.Zero, Vector3.UnitY)]),
            ],
        });

        Assert.Equal(GraphVisualizationKind.AzimuthPolarHeatmap, result.VisualizationKind);
        Assert.NotNull(result.Heatmap);
        Assert.True(result.Heatmap!.Values.Cast<double>().Any(value => value > 0));
    }

    [Fact]
    public void AzimuthPolarHeatmapGraphType_RequiresExactlyOneSource()
    {
        var graphType = new AzimuthPolarHeatmapGraphType();
        var context = new GraphBuildContext { Sources = [] };

        var ex = Assert.Throws<InvalidOperationException>(() => graphType.Build(context));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BarGraphType_BuildsGroupedSeriesForMultipleSources()
    {
        var type = new AngleBinBarChartGraphType();
        var result = type.Build(new GraphBuildContext
        {
            AngleBinSizeDeg = 90,
            Sources =
            [
                BuildSource("s1", GraphableSourceKind.CylindricalLightSource, [new Ray3D(Vector3.Zero, Vector3.UnitX)]),
                BuildSource("s2", GraphableSourceKind.ProjectionResult, [new Ray3D(Vector3.Zero, Vector3.UnitY)]),
            ],
        });

        Assert.Equal(GraphVisualizationKind.AngleGroupedBar, result.VisualizationKind);
        Assert.Equal(2, result.Series.Count);
        Assert.Equal(2, result.Series[0].Bins.Count);
    }

    [Fact]
    public void AzimuthBarGraphType_BuildsGroupedSeries()
    {
        var type = new AzimuthBinBarChartGraphType();
        var result = type.Build(new GraphBuildContext
        {
            AzimuthBinSizeDeg = 90,
            Sources =
            [
                BuildSource("s1", GraphableSourceKind.CylindricalLightSource, [new Ray3D(Vector3.Zero, Vector3.UnitY)]),
                BuildSource("s2", GraphableSourceKind.ProjectionResult, [new Ray3D(Vector3.Zero, Vector3.UnitZ)]),
            ],
        });

        Assert.Equal(GraphVisualizationKind.AzimuthGroupedBar, result.VisualizationKind);
        Assert.Equal(2, result.Series.Count);
        Assert.Equal(1, result.Series[0].Bins[0].Count);
        Assert.Equal(1, result.Series[1].Bins[1].Count);
    }

    [Fact]
    public void LineFocus_ComputesBounds_AndHandlesConstantSeries()
    {
        var bounds = LineGraphYAxisFocusService.TryComputeBounds(
            [
                new GraphSeriesData
                {
                    Name = "a",
                    Points = [new ScatterPointData(0, 10), new ScatterPointData(1, 20)],
                },
            ]);

        Assert.NotNull(bounds);
        Assert.True(bounds.Value.Min < 10);
        Assert.True(bounds.Value.Max > 20);

        var constant = LineGraphYAxisFocusService.TryComputeBounds(
            [
                new GraphSeriesData
                {
                    Name = "constant",
                    Points = [new ScatterPointData(0, 5), new ScatterPointData(1, 5)],
                },
            ]);

        Assert.NotNull(constant);
        Assert.True(constant.Value.Max > constant.Value.Min);
    }

    [Fact]
    public void StoredGraphChartSession_AddSelectDelete_Works_WithExtendedParameters()
    {
        var session = new StoredGraphChartSession();
        session.AddAndSelect(new StoredGraphChart
        {
            Id = "c1",
            DisplayName = "Chart 1",
            GraphTypeId = "graph.angle-bin-bar",
            AngleBinSizeDeg = 15,
            AzimuthBinSizeDeg = null,
            PolarBinSizeDeg = null,
            SelectedSourceIds = ["s1"],
        });

        session.AddAndSelect(new StoredGraphChart
        {
            Id = "c2",
            DisplayName = "Chart 2",
            GraphTypeId = "graph.azimuth-polar-heatmap",
            AngleBinSizeDeg = 10,
            AzimuthBinSizeDeg = 20,
            PolarBinSizeDeg = 30,
            SelectedSourceIds = ["s2"],
        });

        Assert.True(session.Select("c1"));
        var deleted = session.DeleteSelected();
        Assert.Equal("c1", deleted?.Id);
        Assert.Equal("c2", session.SelectedChart?.Id);
    }

    [Fact]
    public void Extraction_ProvidesFrameAxesForCylindricalAndProjection()
    {
        var service = new GraphSourceExtractionService(new CylindricalRayGenerator());
        var source = new CylindricalLightSource(
            "Lamp A",
            new Frame3D(Vector3.Zero, Quaternion.Identity),
            radius: 1f,
            height: 2f,
            rayCount: 4);

        var projectionResult = new NamedProjectionResultState
        {
            Key = "p1",
            DisplayName = "Projection 1",
            Result = new ProjectionComputationResult
            {
                MethodId = ProjectionMethodIds.PointSource,
                PointSourceOrigin = new Point3(0, 0, 0),
                SourceFrame = new PointSourceFrameState
                {
                    Origin = new Point3(0, 0, 0),
                    AxisX = new Vector3D(1, 0, 0),
                    AxisY = new Vector3D(0, 1, 0),
                    AxisZ = new Vector3D(0, 0, 1),
                },
                Rays = [new ProjectionRay(new Ray3D(Vector3.Zero, Vector3.UnitX), new Point3(0, 0, 0))],
            },
        };

        var extracted = service.Extract(
            [
                new GraphSceneData
                {
                    SceneName = "Scene One",
                    CylindricalSources = [source],
                    ProjectionResults = [projectionResult],
                },
            ]);

        Assert.All(extracted, s => Assert.True(s.AxisY.LengthSquared() > 0 && s.AxisZ.LengthSquared() > 0));
    }


    [Fact]
    public void Extraction_UsesCylindricalProjectionMetadataForSourceLengthAndRays()
    {
        var service = new GraphSourceExtractionService(new CylindricalRayGenerator());
        var result = new NamedProjectionResultState
        {
            Key = "proj-cyl",
            DisplayName = "Cyl Projection",
            Result = new ProjectionComputationResult
            {
                MethodId = ProjectionMethodIds.CylindricalSource,
                SourceFrame = new PointSourceFrameState
                {
                    Origin = new Point3(0, 0, 0),
                    AxisX = new Vector3D(1, 0, 0),
                    AxisY = new Vector3D(0, 1, 0),
                    AxisZ = new Vector3D(0, 0, 1),
                },
                Rays = Array.Empty<ProjectionRay>(),
                CylindricalSource = new CylindricalProjectionState
                {
                    SourceFrame = new PointSourceFrameState
                    {
                        Origin = new Point3(0, 0, 0),
                        AxisX = new Vector3D(1, 0, 0),
                        AxisY = new Vector3D(0, 1, 0),
                        AxisZ = new Vector3D(0, 0, 1),
                    },
                    Radius = 2,
                    Length = 9,
                    Points =
                    [
                        new CylindricalProjectionPoint(
                            new Point3(2, 4, 0),
                            new Point3(0, 2, 0),
                            new Vector3D(1, 0, 0),
                            new Point3(0, 2, 0)),
                    ],
                },
            },
        };

        var extracted = service.Extract(
        [
            new GraphSceneData
            {
                SceneName = "Scene C",
                CylindricalSources = [],
                ProjectionResults = [result],
            },
        ]);

        var projection = Assert.Single(extracted);
        Assert.Equal(9d, projection.SourceLength);
        Assert.Single(projection.Rays);
    }

    private static GraphableSourceData BuildSource(string id, GraphableSourceKind kind, IReadOnlyList<Ray3D> rays)
    {
        return new GraphableSourceData
        {
            Id = id,
            DisplayName = id,
            Kind = kind,
            AxisX = Vector3.UnitX,
            AxisY = Vector3.UnitY,
            AxisZ = Vector3.UnitZ,
            Rays = rays,
        };
    }
}
