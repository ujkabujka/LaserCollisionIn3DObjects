using System.Collections.ObjectModel;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.Scene;

public sealed class SceneProjectionState
{
    public string SelectedMethodId { get; set; } = ProjectionWorkspaceState.DefaultMethodId;

    public string? SelectedResultKey { get; set; }

    public ObservableCollection<NamedProjectionResultState> SavedResults { get; } = new();

    public ProjectionComputationResult? SelectedResult =>
        SelectedResultKey is null
            ? null
            : SavedResults.FirstOrDefault(result => result.Key == SelectedResultKey)?.Result;
}
