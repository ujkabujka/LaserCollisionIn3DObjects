using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public static class SceneProjectionStateUpdater
{
    public static NamedProjectionResultState SaveResult(SceneProjectionState state, string displayName, ProjectionComputationResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(result);

        var key = CreateStableResultKey(displayName);
        var namedResult = new NamedProjectionResultState
        {
            Key = key,
            DisplayName = displayName.Trim(),
            Result = result,
        };

        state.SelectedMethodId = result.MethodId;
        state.SavedResults.Add(namedResult);
        state.SelectedResultKey = key;
        return namedResult;
    }

    private static string CreateStableResultKey(string displayName)
    {
        return $"projection.{displayName.Trim().ToLowerInvariant().Replace(' ', '-')}.{Guid.NewGuid():N}";
    }
}
