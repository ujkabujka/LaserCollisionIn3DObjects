using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.Scene;

public sealed class SceneProjectionState
{
    public string SelectedMethodId { get; set; } = ProjectionWorkspaceState.DefaultMethodId;

    public string? SelectedResultKey { get; set; }

    public List<NamedProjectionResultState> SavedResults { get; } = new();

    public ProjectionComputationResult? SelectedResult =>
        SelectedResultKey is null
            ? null
            : SavedResults.FirstOrDefault(result => result.Key == SelectedResultKey)?.Result;
}
