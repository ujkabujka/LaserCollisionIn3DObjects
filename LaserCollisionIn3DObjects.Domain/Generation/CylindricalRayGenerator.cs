using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Generation;

/// <summary>
/// Generates rays from the curved surface of a cylindrical light source.
/// </summary>
public sealed class CylindricalRayGenerator
{
    private readonly AxisymmetricRayGenerator _axisymmetricRayGenerator = new();

    /// <summary>
    /// Generates deterministic rays whose origins form a cylindrical shell.
    /// </summary>
    public List<Ray3D> Generate(CylindricalLightSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var axisymmetricSource = new AxisymmetricLightSource(
            source.Name,
            source.Frame,
            AxisymmetricSourceKind.Cylinder,
            new CylindricalSourceProfile(source.Radius, source.Height),
            source.RayCount,
            source.TiltWeight,
            source.TiltPointLocal);

        return _axisymmetricRayGenerator.Generate(axisymmetricSource);
    }
}
