using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class CollisionSourceOwnershipTests
{
    [Fact]
    public void Scene_HasZeroOrOneDeepSourceSnapshot()
    {
        var service = new SceneCollectionService();
        var scene = service.CreateScene("Scene");
        Assert.Null(scene.AssignedSource);

        var a = service.AddToLibrary(Generated("A", 100, 1));
        var b = service.AddToLibrary(Generated("B", 200, 2));
        service.AssignSource(scene, a);
        var oldSnapshot = scene.AssignedSource;
        service.AssignSource(scene, b);

        Assert.Equal("B", scene.AssignedSource?.Name);
        Assert.NotSame(b, scene.AssignedSource);
        Assert.NotSame(oldSnapshot, scene.AssignedSource);
        Assert.Equal(2, service.AvailableSources.Count);
        Assert.Single(scene.LightSources);
        Assert.Empty(scene.ProjectedLightSources);
    }

    [Fact]
    public void SameLibrarySource_AssignedToTwoScenes_RemainsIndependent()
    {
        var service = new SceneCollectionService();
        var one = service.CreateScene("One");
        var two = service.CreateScene("Two");
        var library = service.AddToLibrary(Generated("A", 100, 1));
        service.AssignSource(one, library);
        service.AssignSource(two, library);

        one.AssignedSource!.GeneratedSource!.PositionX = 99;
        one.AssignedSource.GeneratedSource.HybridSegments.Add(new HybridSourceSegmentItemViewModel());

        Assert.Equal(1, library.GeneratedSource!.PositionX);
        Assert.Equal(1, two.AssignedSource!.GeneratedSource!.PositionX);
        Assert.Empty(library.GeneratedSource.HybridSegments);
        Assert.Empty(two.AssignedSource.GeneratedSource.HybridSegments);
    }

    [Fact]
    public void PrismSelection_DoesNotChangeAssignedSource()
    {
        var scene = new CollisionSceneViewModel("Scene");
        scene.AssignSource(Generated("A", 5, 0));
        var assigned = scene.AssignedSource;
        scene.SelectedPrism = new PrismItemViewModel { Name = "Panel" };
        Assert.Same(assigned, scene.AssignedSource);
    }

    private static CollisionSourceLibraryItemViewModel Generated(string name, int rays, float x) => new()
    {
        GeneratedSource = new CylindricalLightSourceItemViewModel { Name = name, RayCount = rays, PositionX = x },
    };
}
