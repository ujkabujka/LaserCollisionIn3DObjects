using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Rendering.Helix;

namespace LaserCollisionIn3DObjects.Wpf.Services;

public sealed class ProjectionRenderSyncService
{
    private readonly ModelVisual3D _dynamicVisualRoot = new();
    private readonly HelixSceneBuilder _sceneBuilder = new();

    public ProjectionRenderSyncService(HelixViewport3D viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        viewport.Children.Add(_dynamicVisualRoot);
    }

    public void SyncProjectionScene(IReadOnlyList<Point3> holePoints, ProjectionComputationResult? projectionResult)
    {
        var visuals = _sceneBuilder.BuildProjectionVisuals(holePoints, projectionResult);
        _dynamicVisualRoot.Children.Clear();
        foreach (var visual in visuals)
        {
            _dynamicVisualRoot.Children.Add(visual);
        }
    }
}
