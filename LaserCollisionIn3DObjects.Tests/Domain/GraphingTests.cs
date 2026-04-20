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

        Assert.Equal(GraphVisualizationKind.Xy, result.VisualizationKind);
        Assert.Single(result.Series);
        Assert.Equal(4, result.Series[0].Bins.Count);
        Assert.Equal(1, result.Series[0].Bins[0].Count);
        Assert.Equal(1, result.Series[0].Bins[2].Count);
    }

    [Fact]
    public void Registry_ResolvesById()
    {
        var registry = new GraphTypeRegistry(new IGraphType[]
        {
            new AngleBinBarChartGraphType(),
            new AngleBinXyChartGraphType(),
        });

        var resolved = registry.Resolve("graph.angle-bin-xy");
        Assert.IsType<AngleBinXyChartGraphType>(resolved);
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
}
