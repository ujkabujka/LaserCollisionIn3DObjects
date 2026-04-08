using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Rendering.Scene;

/// <summary>
/// Rendering-facing scene data container.
/// </summary>
public sealed class SceneModel
{
    /// <summary>
    /// Gets rays to draw in the viewport.
    /// </summary>
    public IList<Ray3D> Rays { get; } = new List<Ray3D>();

    /// <summary>
    /// Gets rectangular prisms to draw in the viewport.
    /// </summary>
    public IList<RectangularPrism> Prisms { get; } = new List<RectangularPrism>();
}
