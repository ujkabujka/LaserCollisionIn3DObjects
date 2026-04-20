using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Scene;
using System.Collections.Specialized;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public class SceneProjectionStateUpdaterTests
{
    [Fact]
    public void SaveResult_RepeatedCalls_AddMultipleEntries()
    {
        var state = new SceneProjectionState();

        SceneProjectionStateUpdater.SaveResult(state, "R1", CreateResult(new Point3(0, 0, 0)));
        SceneProjectionStateUpdater.SaveResult(state, "R2", CreateResult(new Point3(1, 0, 0)));

        Assert.Equal(2, state.SavedResults.Count);
        Assert.Equal("R2", state.SavedResults.Last().DisplayName);
    }

    [Fact]
    public void SavedResults_ObservableCollection_RaisesCollectionChangedOnAdds()
    {
        var state = new SceneProjectionState();
        var events = new List<NotifyCollectionChangedAction>();
        state.SavedResults.CollectionChanged += (_, e) => events.Add(e.Action);

        SceneProjectionStateUpdater.SaveResult(state, "R1", CreateResult(new Point3(0, 0, 0)));
        SceneProjectionStateUpdater.SaveResult(state, "R2", CreateResult(new Point3(1, 0, 0)));

        Assert.Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add }, events);
    }

    [Fact]
    public void DeleteResult_RemovesSelectedAndSelectsNextWhenAvailable()
    {
        var state = new SceneProjectionState();
        var first = SceneProjectionStateUpdater.SaveResult(state, "R1", CreateResult(new Point3(0, 0, 0)));
        var second = SceneProjectionStateUpdater.SaveResult(state, "R2", CreateResult(new Point3(1, 0, 0)));
        var third = SceneProjectionStateUpdater.SaveResult(state, "R3", CreateResult(new Point3(2, 0, 0)));

        state.SelectedResultKey = second.Key;
        var deleted = SceneProjectionStateUpdater.DeleteResult(state, second);

        Assert.True(deleted);
        Assert.DoesNotContain(state.SavedResults, item => item.Key == second.Key);
        Assert.Equal(third.Key, state.SelectedResultKey);
        Assert.Equal(2, state.SavedResults.Count);
    }

    [Fact]
    public void DeleteResult_RemovesLastAndSelectsPreviousWhenNeeded()
    {
        var state = new SceneProjectionState();
        var first = SceneProjectionStateUpdater.SaveResult(state, "R1", CreateResult(new Point3(0, 0, 0)));
        var second = SceneProjectionStateUpdater.SaveResult(state, "R2", CreateResult(new Point3(1, 0, 0)));

        state.SelectedResultKey = second.Key;
        var deleted = SceneProjectionStateUpdater.DeleteResult(state, second);

        Assert.True(deleted);
        Assert.Equal(first.Key, state.SelectedResultKey);
    }

    [Fact]
    public void DeleteResult_RemovesOnlyResultAndClearsSelection()
    {
        var state = new SceneProjectionState();
        var only = SceneProjectionStateUpdater.SaveResult(state, "R1", CreateResult(new Point3(0, 0, 0)));

        state.SelectedResultKey = only.Key;
        var deleted = SceneProjectionStateUpdater.DeleteResult(state, only);

        Assert.True(deleted);
        Assert.Empty(state.SavedResults);
        Assert.Null(state.SelectedResultKey);
    }

    private static ProjectionComputationResult CreateResult(Point3 pointSourceOrigin)
    {
        return new ProjectionComputationResult
        {
            MethodId = ProjectionMethodIds.PointSource,
            PointSourceOrigin = pointSourceOrigin,
            SourceFrame = new PointSourceFrameState
            {
                Origin = new Point3(10, 0, 0),
                AxisX = new Vector3D(1, 0, 0),
                AxisY = new Vector3D(0, 1, 0),
                AxisZ = new Vector3D(0, 0, 1),
            },
            Rays = [new ProjectionRay(new Ray3D(Vector3.Zero, Vector3.UnitX), new Point3(1, 0, 0))],
        };
    }
}
