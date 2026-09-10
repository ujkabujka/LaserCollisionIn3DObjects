using System.Globalization;
using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Collision;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Scene;
using LaserCollisionIn3DObjects.Rendering.Helix;
using LaserCollisionIn3DObjects.Wpf.Converters;
using LaserCollisionIn3DObjects.Wpf.Services;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using Xunit;

namespace LaserCollisionIn3DObjects.Wpf.Tests;

public sealed class CollisionDisplayPresentationTests
{
    [Fact]
    public void HelixOptionsIndependentlySuppressRayAndHitVisualBatches()
    {
        var ray = new Ray3D(Vector3.Zero, Vector3.UnitX);
        var scene = new SceneModel();
        scene.Rays.Add(ray);
        var hits = new Dictionary<Ray3D, RayHitResult> { [ray] = RayHitResult.Hit(2, new Vector3(2, 0, 0), -Vector3.UnitX, null) };
        var builder = new HelixSceneBuilder();
        var all = builder.BuildVisuals(scene, hits);
        var noRays = builder.BuildVisuals(scene, hits, options: new(false, true));
        var noHits = builder.BuildVisuals(scene, hits, options: new(true, false));
        var neither = builder.BuildVisuals(scene, hits, options: new(false, false));
        Assert.Equal(all.Count - 1, noRays.Count);
        Assert.Equal(all.Count - 1, noHits.Count);
        Assert.Equal(all.Count - 2, neither.Count);
        Assert.Single(hits);
        Assert.True(hits[ray].HasHit);
    }

    [Fact]
    public void SceneDisplayFlagsAreIndependentAndDoNotInvalidateResults()
    {
        var scene = new CollisionSceneViewModel("Front Test");
        scene.PublishCollisionResults([], [], null);
        scene.ShowCollisionRays = false;
        scene.ShowCollisionHitPoints = true;
        Assert.True(scene.HasValidCollisionRun);
        Assert.False(scene.ShowCollisionRays);
        Assert.True(scene.ShowCollisionHitPoints);
    }

    [Fact]
    public void OrdinalsFollowCollisionCollectionOrderAfterDeletionAndIgnoreNames()
    {
        var service = new SceneCollectionService();
        var first = service.CreateScene("Front Test");
        var second = service.CreateScene("Anything");
        Assert.Equal(1, first.SceneOrdinal);
        Assert.Equal(2, second.SceneOrdinal);
        service.RemoveScene(first);
        Assert.Equal(1, second.SceneOrdinal);
    }

    [Theory]
    [InlineData(1, "Prism 1", 30, "S1 Prism 1 - 30 hits")]
    [InlineData(2, "Detector A", 17, "S2 Detector A - 17 hits")]
    [InlineData(1, "Prism 3", 0, "S1 Prism 3 - 0 hits")]
    public void PanelLabelUsesOrdinalRealNameAndHitCount(int ordinal, string panel, int hits, string expected)
    {
        var actual = new PanelResultLabelConverter().Convert([ordinal, panel, hits], typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, actual);
    }
}
