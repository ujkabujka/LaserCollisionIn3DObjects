using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CollisionViewportRefreshCoordinatorTests
{
    [Fact]
    public void RequestsBeforeInitializationDoNotRender_AndInitializationRendersOnce()
    {
        var calls = new List<bool>();
        var coordinator = new CollisionViewportRefreshCoordinator(calls.Add);

        coordinator.Request(false);
        coordinator.Request(true);
        coordinator.Initialize();
        coordinator.Initialize();
        coordinator.Initialize();

        Assert.Equal(new[] { false }, calls);
    }

    [Fact]
    public void RuntimeRequestProducesOneRefreshWithRequestedCollisionMode()
    {
        var calls = new List<bool>();
        var coordinator = new CollisionViewportRefreshCoordinator(calls.Add);
        coordinator.Initialize();
        calls.Clear();

        coordinator.Request(false);

        Assert.Equal(new[] { false }, calls);
    }

    [Fact]
    public void ReentrantRequestsAreCoalescedIntoOneFinalRefresh()
    {
        var calls = new List<bool>();
        CollisionViewportRefreshCoordinator? coordinator = null;
        coordinator = new CollisionViewportRefreshCoordinator(runCollision =>
        {
            calls.Add(runCollision);
            if (calls.Count == 1)
            {
                coordinator.Request(false);
                coordinator.Request(true);
            }
        });

        coordinator.Initialize();

        Assert.Equal(new[] { false, true }, calls);
    }
}
