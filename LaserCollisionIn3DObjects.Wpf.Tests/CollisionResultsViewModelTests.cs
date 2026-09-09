using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class CollisionResultsViewModelTests
{
    [Fact]
    public void SelectingPanel_ReplacesPlot_AndInvalidationClearsEverything()
    {
        var first = new RectangularPrism("A", new Frame3D(), 1, 2, 4);
        var second = new RectangularPrism("B", new Frame3D(), 1, 5, 1);
        var hit = RayHitResult.Hit(1, new Vector3(.5f, -.2f, .7f), Vector3.UnitX, first);
        var analysis = new PanelCollisionAnalysisService().Build("Scene", [first, second],
            [new PanelCollisionInputHit(0, hit, CollisionRaySourceType.Manual, "Ray")], 1);
        var scene = new CollisionSceneViewModel("Scene");

        scene.PublishCollisionResults([], [], analysis);
        var firstPlot = scene.PanelPlotModel;
        scene.SelectedPanelResult = scene.PanelResults[1];

        Assert.NotNull(firstPlot);
        Assert.NotSame(firstPlot, scene.PanelPlotModel);
        Assert.Contains("B", scene.PanelPlotModel!.Title);
        scene.InvalidateCollisionResults();
        Assert.Empty(scene.PanelResults);
        Assert.Null(scene.SelectedPanelResult);
        Assert.Null(scene.PanelPlotModel);
        Assert.False(scene.HasValidCollisionRun);
    }
}
