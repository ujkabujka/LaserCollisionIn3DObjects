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

    public static bool DeleteResult(SceneProjectionState state, NamedProjectionResultState result)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        var index = state.SavedResults.IndexOf(result);
        if (index < 0)
        {
            return false;
        }

        var wasSelected = string.Equals(state.SelectedResultKey, result.Key, StringComparison.Ordinal);
        state.SavedResults.RemoveAt(index);

        if (!wasSelected)
        {
            return true;
        }

        if (state.SavedResults.Count == 0)
        {
            state.SelectedResultKey = null;
            return true;
        }

        var nextIndex = Math.Min(index, state.SavedResults.Count - 1);
        state.SelectedResultKey = state.SavedResults[nextIndex].Key;
        return true;
    }

    private static string CreateStableResultKey(string displayName)
    {
        return $"projection.{displayName.Trim().ToLowerInvariant().Replace(' ', '-')}.{Guid.NewGuid():N}";
    }
}
