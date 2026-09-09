using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class PanelCollisionAnalysisTests
{
    [Fact]
    public void Build_UsesCollisionTimeLocalFrame_IncludesEmptyPanels_AndOrdersHits()
    {
        var frame = new Frame3D(new Vector3(10, 4, 2), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2));
        var panel = new RectangularPrism("Panel, \"A\"", frame, 1, 4, 6);
        var empty = new RectangularPrism("Empty", new Frame3D(), 1, 2, 2);
        var world1 = frame.TransformPointToWorld(new Vector3(.5f, -.2f, .7f));
        var world2 = frame.TransformPointToWorld(new Vector3(.5f, .3f, -.4f));
        var inputs = new[]
        {
            new PanelCollisionInputHit(8, RayHitResult.Hit(2, world2, Vector3.UnitX, panel), CollisionRaySourceType.Manual, "Later"),
            new PanelCollisionInputHit(2, RayHitResult.Hit(1, world1, Vector3.UnitX, panel), CollisionRaySourceType.ImportedLightSource, "Source, \"one\""),
        };

        var result = new PanelCollisionAnalysisService().Build("Scene, 1", [panel, empty], inputs, 12);

        Assert.Equal(2, result.Panels.Count);
        Assert.Equal(0, result.Panels[1].HitCount);
        Assert.Equal([2, 8], result.Panels[0].Hits.Select(x => x.RayIndex));
        Assert.Equal([1, 2], result.Panels[0].Hits.Select(x => x.PanelHitIndex));
        Assert.Equal(-.2f, result.Panels[0].Hits[0].LocalY, 4);
        Assert.Equal(.7f, result.Panels[0].Hits[0].LocalZ, 4);
        Assert.Equal(world1, result.Panels[0].Hits[0].WorldHitPoint);
        Assert.Equal(CollisionRaySourceType.ImportedLightSource, result.Panels[0].Hits[0].SourceType);
    }

    [Fact]
    public void Csv_UsesInvariantRichColumnsAndEscaping()
    {
        var panel = new RectangularPrism("Panel, \"A\"", new Frame3D(), 1, 2, 2);
        var hit = RayHitResult.Hit(1, new Vector3(.5f, -.25f, .75f), Vector3.UnitX, panel);
        var analysis = new PanelCollisionAnalysisService().Build("Scene, 1", [panel],
            [new PanelCollisionInputHit(3, hit, CollisionRaySourceType.Manual, "Source, \"A\"")], 4);

        var csv = new PanelCollisionCsvExportService().CreateCsv(analysis);

        Assert.StartsWith("SceneName,PanelName,PanelHitIndex,RayIndex,SourceType,SourceName,LocalY,LocalZ,WorldX,WorldY,WorldZ", csv);
        Assert.Contains("\"Scene, 1\",\"Panel, \"\"A\"\"\",1,3,Manual,\"Source, \"\"A\"\"\",-0.25,0.75", csv);
    }
}
