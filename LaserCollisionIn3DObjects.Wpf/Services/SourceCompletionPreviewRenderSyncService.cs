using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.SourceCompletion;
using LaserCollisionIn3DObjects.Rendering.Helix;
using LaserCollisionIn3DObjects.Wpf.ViewModels;
using System.Windows.Media.Media3D;
namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class SourceCompletionPreviewRenderSyncService
{
    private readonly HelixViewport3D _viewport;
    private readonly ModelVisual3D _dynamicVisualRoot = new();
    private readonly HelixSceneBuilder _sceneBuilder = new();

    public SourceCompletionPreviewRenderSyncService(HelixViewport3D viewport)
    {
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        _viewport.Children.Add(_dynamicVisualRoot);
    }

    public void SyncPreview(ProjectedLightSourceItemViewModel? selectedSource, ProjectedSourceCompletionResult? completionResult)
    {
        var originalRays = selectedSource?.Rays.Select(ray => ray.Ray).ToList();
        var syntheticRays = completionResult is null ? null : GetSyntheticRays(completionResult).Select(ray => ray.Ray).ToList();
        var sourceFrame = selectedSource?.SourceFrame;
        var profile = selectedSource?.ProfileDefinition.BuildProfile();

        var visuals = _sceneBuilder.BuildSourceCompletionPreviewVisuals(sourceFrame, profile, originalRays, syntheticRays);
        _dynamicVisualRoot.Children.Clear();
        foreach (var visual in visuals)
        {
            _dynamicVisualRoot.Children.Add(visual);
        }

        _viewport.ZoomExtents();
    }

    internal static IReadOnlyList<ProjectionRay> GetSyntheticRays(ProjectedSourceCompletionResult result)
    {
        if (result.Rays.Count >= result.OriginalRayCount + result.SyntheticRayCount)
        {
            return result.Rays.Skip(result.OriginalRayCount).Take(result.SyntheticRayCount).ToList();
        }

        if (result.Rays.Count == result.SyntheticRayCount)
        {
            return result.Rays.ToList();
        }

        return result.Rays.TakeLast(result.SyntheticRayCount).ToList();
    }

    public void SyncCompletedSourcePreview(CompletedSourceItem completedSource)
    {
        ArgumentNullException.ThrowIfNull(completedSource);
        var visuals = _sceneBuilder.BuildSourceCompletionPreviewVisuals(
            completedSource.SourceFrame,
            completedSource.ProfileDefinition.BuildProfile(),
            completedSource.OriginalRays.Select(ray => ray.Ray).ToList(),
            completedSource.SyntheticRays.Select(ray => ray.Ray).ToList());
        _dynamicVisualRoot.Children.Clear();
        foreach (var visual in visuals) _dynamicVisualRoot.Children.Add(visual);
        _viewport.ZoomExtents();
    }
}
