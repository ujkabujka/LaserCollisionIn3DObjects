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
    /// Gets additional axisymmetric (non-cylindrical) light sources in the scene.
    /// </summary>
    public List<AxisymmetricLightSource> AxisymmetricLightSources { get; } = new();

    /// <summary>
    /// Gets the rays in the scene.
    /// </summary>
    public List<Ray3D> Rays { get; } = new();

    /// <summary>
    /// Gets the rays generated from cylindrical light sources.
    /// </summary>
    public List<Ray3D> GeneratedRays { get; } = new();

    /// <summary>
    /// Gets exact candidate rays copied from projected light-source results.
    /// These rays are used for collision. Rendering should always show their origins,
    /// but should only show line segments for rays that hit.
    /// </summary>
    public List<Ray3D> ProjectedSourceRays { get; } = new();


    /// <summary>
    /// Gets the hole centers from prisms .
    /// </summary>
    public List<Point3> HolePoints { get; } = new();
    /// <summary>Gets naturally occurring annotated measurement points.</summary>
    public List<Point3> NaturalPoints { get; } = new();
}
