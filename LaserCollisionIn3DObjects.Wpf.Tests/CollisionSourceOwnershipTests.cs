using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using LaserCollisionIn3DObjects.Domain.Geometry;
using System.Numerics;
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
        Assert.NotNull(scene.AssignedSource?.GeneratedSource);
        Assert.Null(scene.AssignedSource?.TransferredSource);
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

    [Fact]
    public void CollisionScene_ExposesNoManualRayOrMultiSourceModel()
    {
        var properties = typeof(CollisionSceneViewModel).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("Rays", properties);
        Assert.DoesNotContain("Selected" + "Ray", properties);
        Assert.DoesNotContain("LightSources", properties);
        Assert.DoesNotContain("ProjectedLightSources", properties);
        Assert.DoesNotContain("SelectedLightSource", properties);
        Assert.DoesNotContain("SelectedProjectedLightSource", properties);
    }

    [Fact]
    public void TransferredSource_AssignmentPreservesFrameAndExactRaysInAnIndependentSnapshot()
    {
        var service = new SceneCollectionService();
        var scene = service.CreateScene("Scene");
        var transferred = new ProjectedLightSourceItemViewModel
        {
            Name = "Transferred",
            SourceFrame = new() { Origin = new(1, 2, 3), AxisX = new(0, 1, 0), AxisY = new(-1, 0, 0), AxisZ = new(0, 0, 1) },
        };
        transferred.ExactRays.Add(new Ray3D(new Vector3(4, 5, 6), Vector3.UnitY));
        var library = service.AddToLibrary(new CollisionSourceLibraryItemViewModel { TransferredSource = transferred });

        service.AssignSource(scene, library);
        var snapshot = scene.AssignedSource!.TransferredSource!;

        Assert.NotSame(library.TransferredSource, snapshot);
        Assert.Equal(library.TransferredSource!.SourceFrame, snapshot.SourceFrame);
        Assert.Equal(library.TransferredSource.ExactRays, snapshot.ExactRays);
        snapshot.ExactRays[0] = new Ray3D(Vector3.Zero, Vector3.UnitX);
        Assert.NotEqual(library.TransferredSource.ExactRays[0], snapshot.ExactRays[0]);
    }

    private static CollisionSourceLibraryItemViewModel Generated(string name, int rays, float x) => new()
    {
        GeneratedSource = new CylindricalLightSourceItemViewModel { Name = name, RayCount = rays, PositionX = x },
    };
}
