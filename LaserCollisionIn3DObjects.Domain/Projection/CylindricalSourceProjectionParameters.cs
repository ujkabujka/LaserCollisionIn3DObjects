using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record CylindricalSourceProjectionParameters(
    Point3 SourceFrameOrigin,
    Vector3D SourceFrameX,
    Vector3D SourceFrameY,
    double Radius,
    double Length) : IProjectionParameters
{
    public AxisymmetricProfileDefinition ProfileDefinition { get; init; } =
        new CylindricalAxisymmetricProfileDefinition(Radius, Length);
}

public interface AxisymmetricProfileDefinition
{
    IAxisymmetricSourceProfile BuildProfile();
}

public sealed record CylindricalAxisymmetricProfileDefinition(double Radius, double Length) : AxisymmetricProfileDefinition
{
    public IAxisymmetricSourceProfile BuildProfile() => new CylindricalSourceProfile((float)Radius, (float)Length);
}
