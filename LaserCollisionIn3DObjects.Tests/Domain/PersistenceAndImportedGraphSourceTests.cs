using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Graphing;
using LaserCollisionIn3DObjects.Domain.Persistence;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class PersistenceAndImportedGraphSourceTests
{
    [Fact]
    public void VersionOneMigratesAndFutureVersionIsRejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"schemaVersion\":1,\"scenes\":[]}");
            var migrated = new JsonStateFileService().LoadProject(path);
            Assert.Equal(ProjectState.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.Empty(migrated.GraphicMaster.ImportedSources);

            File.WriteAllText(path, $"{{\"schemaVersion\":{ProjectState.CurrentSchemaVersion + 1}}}");
            var error = Assert.Throws<NotSupportedException>(() => new JsonStateFileService().LoadProject(path));
            Assert.Contains("newer than the supported version", error.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OldSceneDisplayOptionsDefaultToVisibleAndRoundTripIndependently()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"schemaVersion\":1,\"scenes\":[{\"name\":\"old\"}]}");
            var oldScene = Assert.Single(new JsonStateFileService().LoadProject(path).Scenes);
            Assert.True(oldScene.ShowCollisionRays);
            Assert.True(oldScene.ShowCollisionHitPoints);

            var state = new ProjectState { Scenes = [new SceneState { Name = "one", ShowCollisionRays = false, ShowCollisionHitPoints = true }, new SceneState { Name = "two", ShowCollisionRays = true, ShowCollisionHitPoints = false }] };
            new JsonStateFileService().SaveProject(path, state);
            var scenes = new JsonStateFileService().LoadProject(path).Scenes;
            Assert.False(scenes[0].ShowCollisionRays);
            Assert.True(scenes[0].ShowCollisionHitPoints);
            Assert.True(scenes[1].ShowCollisionRays);
            Assert.False(scenes[1].ShowCollisionHitPoints);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GraphicMasterDataRoundTripsWithoutOriginalFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var source = CreateTransfer();
            var state = new ProjectState { GraphicMaster = new GraphicMasterState
            {
                ImportedSources = [new() { Id = "imported::stable", OriginalFileName = "gone.laser-source.txt", Source = source }],
                StoredCharts = [new() { Id = "chart", DisplayName = "Angles", GraphTypeId = "graph.angle-bin-bar", AngleBinSizeDeg = 5, SelectedSourceIds = ["imported::stable"] }],
            }};
            new JsonStateFileService().SaveProject(path, state);
            var restored = new JsonStateFileService().LoadProject(path);
            Assert.Equal("imported::stable", Assert.Single(restored.GraphicMaster.ImportedSources).Id);
            Assert.Equal("imported::stable", Assert.Single(Assert.Single(restored.GraphicMaster.StoredCharts).SelectedSourceIds));
            Assert.Equal(2, restored.GraphicMaster.ImportedSources[0].Source!.Rays.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ImportedTransferUsesFrameForWorldRaysAndAxes()
    {
        var transfer = CreateTransfer();
        var graph = new ImportedGraphSourceConverter().Convert("imported::1", transfer);
        Assert.Equal(transfer.Name, graph.DisplayName.Replace("Imported / ", ""));
        Assert.Equal(transfer.Rays.Count, graph.Rays.Count);
        AssertVector(transfer.Frame.TransformDirectionToWorld(Vector3.UnitX), graph.AxisX);
        AssertVector(transfer.Frame.TransformDirectionToWorld(transfer.Rays[0].DirectionLocal), graph.Rays[0].Direction);
        AssertVector(transfer.Frame.TransformPointToWorld(transfer.Rays[0].PositionLocal), graph.Rays[0].Origin);
        Assert.NotEmpty(new AngleBinBarChartGraphType().Build(new GraphBuildContext { Sources = [graph] }).Series);
        Assert.NotEmpty(new AzimuthBinBarChartGraphType().Build(new GraphBuildContext { Sources = [graph] }).Series);
        Assert.NotNull(new AzimuthPolarHeatmapGraphType().Build(new GraphBuildContext { Sources = [graph] }).Heatmap);
    }

    private static LightSourceTransferData CreateTransfer()
    {
        var frame = new Frame3D(new Vector3(10, 20, 30), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2));
        return new("Portable", new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 2, Length = 4 }, frame, 0, Vector3.Zero,
            [new(new Vector3(1, 2, 3), Vector3.UnitX), new(Vector3.Zero, Vector3.UnitY)]);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(Vector3.Distance(expected, actual), 0, 1e-5f);
    }
}
