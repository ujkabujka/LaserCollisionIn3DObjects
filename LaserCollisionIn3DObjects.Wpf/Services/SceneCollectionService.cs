using System.Collections.ObjectModel;
using LaserCollisionIn3DObjects.Wpf.Infrastructure;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Services;

/// <summary>
/// Shared scene state that can be reused by multiple workspaces.
/// </summary>
public sealed class SceneCollectionService : ObservableObject
{
    public event EventHandler? SceneContentChanged;
    private CollisionSceneViewModel? _selectedScene;

    public ObservableCollection<CollisionSceneViewModel> Scenes { get; } = new();

    public CollisionSceneViewModel? SelectedScene
    {
        get => _selectedScene;
        set => SetProperty(ref _selectedScene, value);
    }

    public CollisionSceneViewModel CreateScene(string? name = null)
    {
        var resolvedName = string.IsNullOrWhiteSpace(name)
            ? $"Scene {Scenes.Count + 1}"
            : name.Trim();

        var scene = new CollisionSceneViewModel(resolvedName);
        AddScene(scene);
        return scene;
    }

    public string CreateUniqueSceneName(string baseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        if (Scenes.All(scene => !string.Equals(scene.Name, baseName, StringComparison.OrdinalIgnoreCase))) return baseName;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";
            if (Scenes.All(scene => !string.Equals(scene.Name, candidate, StringComparison.OrdinalIgnoreCase))) return candidate;
        }
    }

    public void AddScene(CollisionSceneViewModel scene, bool selectScene = true)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Scenes.Add(scene);

        if (selectScene || SelectedScene is null)
        {
            SelectedScene = scene;
        }
    }

    public void NotifySceneContentChanged() => SceneContentChanged?.Invoke(this, EventArgs.Empty);

    public bool RemoveScene(CollisionSceneViewModel scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var removedIndex = Scenes.IndexOf(scene);
        if (removedIndex < 0)
        {
            return false;
        }

        var wasSelected = ReferenceEquals(SelectedScene, scene);
        Scenes.RemoveAt(removedIndex);

        if (!wasSelected)
        {
            return true;
        }

        if (Scenes.Count == 0)
        {
            SelectedScene = null;
            return true;
        }

        var nextIndex = Math.Min(removedIndex, Scenes.Count - 1);
        SelectedScene = Scenes[nextIndex];
        return true;
    }
}
