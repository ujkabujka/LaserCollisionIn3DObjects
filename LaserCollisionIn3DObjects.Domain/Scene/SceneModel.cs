using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Scene;

/// <summary>
/// Represents a rendering-independent scene containing geometry and rays.
/// </summary>
public sealed class SceneModel
{
    /// <summary>
    /// Gets the rectangular prisms in the scene.
    /// </summary>
    public List<RectangularPrism> RectangularPrisms { get; } = new();

    /// <summary>
    /// Gets the cylindrical light sources in the scene.
    /// </summary>
    public List<CylindricalLightSource> CylindricalLightSources { get; } = new();

    /// <summary>
    /// Gets the rays in the scene.
    /// </summary>
    public List<Ray3D> Rays { get; } = new();

    /// <summary>
    /// Gets the rays generated from cylindrical light sources.
    /// </summary>
    public List<Ray3D> GeneratedRays { get; } = new();
}
