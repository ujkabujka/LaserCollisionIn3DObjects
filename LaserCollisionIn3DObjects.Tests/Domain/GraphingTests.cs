using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Domain.Projection;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class GraphingTests
{
    [Fact]
    public void CalculateAngleDegrees_UsesSourceXAxis_AndStaysInRange()
    {
        var angle0 = AngleHistogramService.CalculateAngleDegrees(Vector3.UnitX, Vector3.UnitX);
        var angle90 = AngleHistogramService.CalculateAngleDegrees(Vector3.UnitX, Vector3.UnitY);
        var angle180 = AngleHistogramService.CalculateAngleDegrees(Vector3.UnitX, -Vector3.UnitX);

        Assert.InRange(angle0, 0, 180);
        Assert.InRange(angle90, 0, 180);
        Assert.InRange(angle180, 0, 180);
        Assert.Equal(0, angle0, 5);
        Assert.Equal(90, angle90, 5);
        Assert.Equal(180, angle180, 5);
    }

    [Fact]
    public void CreateHistogram_UsesLastBinFor180Degrees()
    {
        var service = new AngleHistogramService();
        var rays = new[]
        {
            new Ray3D(Vector3.Zero, Vector3.UnitX),
            new Ray3D(Vector3.Zero, -Vector3.UnitX),
        };

        var bins = service.CreateHistogram(rays, Vector3.UnitX, 10);
        Assert.Equal(18, bins.Count);
        Assert.Equal(1, bins[0].Count);
        Assert.Equal(1, bins[^1].Count);
    }

    [Fact]
    public void BarGraphType_BuildsGroupedSeriesForMultipleSources()
    {
        var type = new AngleBinBarChartGraphType();
        var result = type.Build(new GraphBuildContext
        {
            BinSizeDeg = 90,
            Sources =
            [
                new GraphableSourceData
                {
                    Id = "s1",
                    DisplayName = "S1",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = Vector3.UnitX,
                    Rays = [new Ray3D(Vector3.Zero, Vector3.UnitX)],
                },
                new GraphableSourceData
                {
                    Id = "s2",
                    DisplayName = "S2",
                    Kind = GraphableSourceKind.ProjectionResult,
                    AxisX = Vector3.UnitX,
                    Rays = [new Ray3D(Vector3.Zero, Vector3.UnitY)],
                },
            ],
        });

        Assert.Equal(GraphVisualizationKind.GroupedBar, result.VisualizationKind);
        Assert.Equal(2, result.Series.Count);
        Assert.Equal(2, result.Series[0].Bins.Count);
        Assert.Equal(1, result.Series[0].Bins[0].Count);
        Assert.Equal(1, result.Series[1].Bins[1].Count);
    }

    [Fact]
    public void XyGraphType_UsesSameBinnedDistribution()
    {
        var type = new AngleBinXyChartGraphType();
        var result = type.Build(new GraphBuildContext
        {
            BinSizeDeg = 45,
            Sources =
            [
                new GraphableSourceData
                {
                    Id = "s1",
                    DisplayName = "S1",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = Vector3.UnitX,
                    Rays =
                    [
                        new Ray3D(Vector3.Zero, Vector3.UnitX),
                        new Ray3D(Vector3.Zero, Vector3.UnitY),
                    ],
                },
            ],
        });

        Assert.Equal(GraphVisualizationKind.AngleBinXyLine, result.VisualizationKind);
        Assert.Single(result.Series);
        Assert.Equal(4, result.Series[0].Bins.Count);
        Assert.Equal(1, result.Series[0].Bins[0].Count);
        Assert.Equal(1, result.Series[0].Bins[2].Count);
    }

    [Fact]
    public void BarGraphType_RepresentsAngleBinsWithRayCounts()
    {
        var type = new AngleBinBarChartGraphType();
        var result = type.Build(new GraphBuildContext
        {
            BinSizeDeg = 30,
            Sources =
            [
                new GraphableSourceData
                {
                    Id = "s1",
                    DisplayName = "S1",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = Vector3.UnitX,
                    Rays = [new Ray3D(Vector3.Zero, Vector3.UnitX), new Ray3D(Vector3.Zero, Vector3.UnitY)],
                },
            ],
        });

        var bins = result.Series[0].Bins;
        Assert.Equal(0, bins[0].BinStartInclusiveDeg);
        Assert.Equal(30, bins[0].BinEndDeg);
        Assert.Equal(1, bins[0].Count);
        Assert.Equal(1, bins[3].Count);
    }

    [Fact]
    public void Registry_ResolvesById()
    {
        var registry = new GraphTypeRegistry(new IGraphType[]
        {
            new AngleBinBarChartGraphType(),
            new AngleBinXyChartGraphType(),
            new CylindricalNormalizedAxialAngleXyGraphType(),
        });

        var resolved = registry.Resolve("graph.angle-bin-xy");
        Assert.IsType<AngleBinXyChartGraphType>(resolved);
    }

    [Fact]
    public void CylindricalNormalizedAxialAngleXy_ExcludesProjectionResultSources()
    {
        var type = new CylindricalNormalizedAxialAngleXyGraphType();
        var result = type.Build(new GraphBuildContext
        {
            BinSizeDeg = 10,
            Sources =
            [
                new GraphableSourceData
                {
                    Id = "proj",
                    DisplayName = "Projection",
                    Kind = GraphableSourceKind.ProjectionResult,
                    AxisX = Vector3.UnitX,
                    FrameOrigin = Vector3.Zero,
                    SourceLength = null,
                    Rays = [new Ray3D(Vector3.Zero, Vector3.UnitX)],
                },
            ],
        });

        Assert.Equal(GraphVisualizationKind.NormalizedAxialAngleXyLine, result.VisualizationKind);
        Assert.Empty(result.Series);
    }

    [Fact]
    public void CylindricalNormalizedAxialAngleXy_ComputesNormalizedPositionAndAngle()
    {
        var type = new CylindricalNormalizedAxialAngleXyGraphType();
        var result = type.Build(new GraphBuildContext
        {
            BinSizeDeg = 10,
            Sources =
            [
                new GraphableSourceData
                {
                    Id = "cyl-1",
                    DisplayName = "Cyl 1",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = Vector3.UnitX,
                    FrameOrigin = Vector3.Zero,
                    SourceLength = 10,
                    Rays =
                    [
                        new Ray3D(new Vector3(2f, 0f, 0f), Vector3.UnitX),
                        new Ray3D(new Vector3(5f, 0f, 0f), Vector3.UnitY),
                    ],
                },
            ],
        });

        Assert.Single(result.Series);
        Assert.Equal(2, result.Series[0].Points.Count);
        Assert.Equal(0.2, result.Series[0].Points[0].X, 5);
        Assert.Equal(0, result.Series[0].Points[0].Y, 5);
        Assert.Equal(0.5, result.Series[0].Points[1].X, 5);
        Assert.Equal(90, result.Series[0].Points[1].Y, 5);
    }

    [Fact]
    public void CylindricalNormalizedAxialAngleXy_UsesMultipleSeriesForMultipleCylinders()
    {
        var type = new CylindricalNormalizedAxialAngleXyGraphType();
        var result = type.Build(new GraphBuildContext
        {
            BinSizeDeg = 10,
            Sources =
            [
                new GraphableSourceData
                {
                    Id = "cyl-1",
                    DisplayName = "Cyl 1",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = Vector3.UnitX,
                    FrameOrigin = Vector3.Zero,
                    SourceLength = 10,
                    Rays = [new Ray3D(new Vector3(1f, 0f, 0f), Vector3.UnitX)],
                },
                new GraphableSourceData
                {
                    Id = "cyl-2",
                    DisplayName = "Cyl 2",
                    Kind = GraphableSourceKind.CylindricalLightSource,
                    AxisX = Vector3.UnitX,
                    FrameOrigin = Vector3.Zero,
                    SourceLength = 5,
                    Rays = [new Ray3D(new Vector3(2.5f, 0f, 0f), Vector3.UnitY)],
                },
            ],
        });

        Assert.Equal(2, result.Series.Count);
        Assert.Equal("Cyl 1", result.Series[0].Name);
        Assert.Equal("Cyl 2", result.Series[1].Name);
    }

    [Fact]
    public void StoredGraphChartSession_AddSelectDelete_Works()
    {
        var session = new StoredGraphChartSession();
        session.AddAndSelect(new StoredGraphChart
        {
            Id = "c1",
            DisplayName = "Chart 1",
            GraphTypeId = "graph.angle-bin-bar",
            BinSizeDeg = 15,
            SelectedSourceIds = ["s1"],
        });
        session.AddAndSelect(new StoredGraphChart
        {
            Id = "c2",
            DisplayName = "Chart 2",
            GraphTypeId = "graph.angle-bin-xy",
            BinSizeDeg = 10,
            SelectedSourceIds = ["s1", "s2"],
        });

        Assert.Equal(2, session.Charts.Count);
        Assert.Equal("c2", session.SelectedChart?.Id);

        Assert.True(session.Select("c1"));
        Assert.Equal("c1", session.SelectedChart?.Id);

        var deleted = session.DeleteSelected();
        Assert.Equal("c1", deleted?.Id);
        Assert.Single(session.Charts);
        Assert.Equal("c2", session.SelectedChart?.Id);
    }

    [Fact]
    public void Extraction_IncludesCylindricalAndProjectionSources()
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
                Rays =
                [
                    new ProjectionRay(new Ray3D(Vector3.Zero, Vector3.UnitX), new Point3(0,0,0)),
                    new ProjectionRay(new Ray3D(Vector3.Zero, Vector3.UnitY), new Point3(0,0,0)),
                ],
            },
        };

        var scenes = new[]
        {
            new GraphSceneData
            {
                SceneName = "Scene One",
                CylindricalSources = [source],
                ProjectionResults = [projectionResult],
            },
        };

        var extracted = service.Extract(scenes);

        Assert.Equal(2, extracted.Count);
        Assert.Contains(extracted, item => item.Kind == GraphableSourceKind.CylindricalLightSource && item.DisplayName.Contains("Cylindrical Source"));
        Assert.Contains(extracted, item => item.Kind == GraphableSourceKind.ProjectionResult && item.DisplayName.Contains("Projection Result"));
        Assert.Contains(extracted, item => item.Kind == GraphableSourceKind.ProjectionResult && item.Rays.Count == 2);
        Assert.Contains(extracted, item => item.Kind == GraphableSourceKind.CylindricalLightSource && item.Rays.Count == 4);
    }

    [Fact]
    public void ProjectionResult_ToPointLaserSource_UsesBeamFrameAndRays()
    {
        var result = new ProjectionComputationResult
        {
            MethodId = ProjectionMethodIds.PointSource,
            PointSourceOrigin = new Point3(1, 2, 3),
            SourceFrame = new PointSourceFrameState
            {
                Origin = new Point3(7, 8, 9),
                AxisX = new Vector3D(1, 0, 0),
                AxisY = new Vector3D(0, 1, 0),
                AxisZ = new Vector3D(0, 0, 1),
            },
            Rays = [new ProjectionRay(new Ray3D(Vector3.Zero, Vector3.UnitX), new Point3(0, 0, 0))],
        };

        var pointLaser = result.ToPointLaserSource();
        Assert.Equal(new Point3(7, 8, 9), pointLaser.Origin);
        Assert.Equal(1, pointLaser.AxisX.X);
        Assert.Single(pointLaser.Rays);
    }

    [Fact]
    public void Extraction_ReflectsProjectionResultAddAndRemove()
    {
        var service = new GraphSourceExtractionService();
        var projectionResults = new List<NamedProjectionResultState>();
        var scene = new GraphSceneData
        {
            SceneName = "Scene One",
            CylindricalSources = [],
            ProjectionResults = projectionResults,
        };

        var before = service.Extract([scene]);
        Assert.Empty(before);

        projectionResults.Add(new NamedProjectionResultState
        {
            Key = "r1",
            DisplayName = "Result 1",
            Result = new ProjectionComputationResult
            {
                MethodId = ProjectionMethodIds.PointSource,
                PointSourceOrigin = new Point3(0, 0, 0),
                SourceFrame = new PointSourceFrameState
                {
                    Origin = new Point3(1, 2, 3),
                    AxisX = new Vector3D(1, 0, 0),
                    AxisY = new Vector3D(0, 1, 0),
                    AxisZ = new Vector3D(0, 0, 1),
                },
                Rays = [new ProjectionRay(new Ray3D(Vector3.Zero, Vector3.UnitX), new Point3(0, 0, 0))],
            },
        });

        var afterAdd = service.Extract([scene]);
        Assert.Single(afterAdd);

        projectionResults.Clear();
        var afterRemove = service.Extract([scene]);
        Assert.Empty(afterRemove);
    }
}
