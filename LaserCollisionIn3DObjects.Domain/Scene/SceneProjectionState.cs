using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.Scene;

public sealed class SceneProjectionState
{
    public string SelectedMethodId { get; set; } = ProjectionWorkspaceState.DefaultMethodId;

    public ProjectionComputationResult? LastResult { get; set; }
}
