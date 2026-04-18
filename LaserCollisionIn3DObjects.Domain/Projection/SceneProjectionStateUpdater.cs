using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public static class SceneProjectionStateUpdater
{
    public static void Apply(SceneProjectionState state, ProjectionComputationResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        state.SelectedMethodId = result.MethodId;
        state.LastResult = result;
    }
}
