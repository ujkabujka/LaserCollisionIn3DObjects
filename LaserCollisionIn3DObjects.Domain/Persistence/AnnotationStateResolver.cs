namespace LaserCollisionIn3DObjects.Domain.Persistence;

public static class AnnotationStateResolver
{
    public static bool IsFolderResolved(AnnotationWorkspaceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrWhiteSpace(state.FolderPath))
        {
            return false;
        }

        return Directory.Exists(state.FolderPath);
    }
}
