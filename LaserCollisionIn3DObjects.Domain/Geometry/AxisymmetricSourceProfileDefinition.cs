using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed record AxisymmetricSourceProfileDefinition
{
    public AxisymmetricSourceKind Kind { get; init; } = AxisymmetricSourceKind.Cylinder;
    public float Radius { get; init; }
    public float Height { get; init; }
    public float Length { get; init; }
    public float RadiusStart { get; init; }
    public float RadiusEnd { get; init; }
    public float ArcRadius { get; init; }
    public OgiveCurvatureDirection OgiveCurvatureDirection { get; init; } = OgiveCurvatureDirection.Outward;
    public IReadOnlyList<HybridAxisymmetricSourceSegmentDefinition> Hybrid { get; init; } = Array.Empty<HybridAxisymmetricSourceSegmentDefinition>();

    public IAxisymmetricSourceProfile BuildProfile() => Kind switch
    {
        AxisymmetricSourceKind.Cylinder => new CylindricalSourceProfile(Radius, Length > 0 ? Length : Height),
        AxisymmetricSourceKind.ConicalFrustum => new ConicalFrustumSourceProfile(RadiusStart, RadiusEnd, Length),
        AxisymmetricSourceKind.CircularOgive => new CircularOgiveSourceProfile(RadiusStart, RadiusEnd, Length, ArcRadius, OgiveCurvatureDirection),
        AxisymmetricSourceKind.Hybrid => new HybridAxisymmetricSourceProfile(Hybrid),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported axisymmetric source kind.")
    };
}
