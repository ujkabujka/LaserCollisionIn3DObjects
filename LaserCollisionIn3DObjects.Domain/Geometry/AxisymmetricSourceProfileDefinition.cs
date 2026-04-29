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

    public IAxisymmetricSourceProfile BuildProfile()
    {
        try
        {
            return Kind switch
            {
                AxisymmetricSourceKind.Cylinder => new CylindricalSourceProfile(Radius, ResolveCylinderLength()),
                AxisymmetricSourceKind.ConicalFrustum => new ConicalFrustumSourceProfile(RadiusStart, RadiusEnd, Length),
                AxisymmetricSourceKind.CircularOgive => new CircularOgiveSourceProfile(RadiusStart, RadiusEnd, Length, ArcRadius, OgiveCurvatureDirection),
                AxisymmetricSourceKind.Hybrid => new HybridAxisymmetricSourceProfile(Hybrid),
                _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported axisymmetric source kind."),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException($"Invalid {Kind} source profile definition: {ex.Message}", ex);
        }
    }

    private float ResolveCylinderLength()
    {
        if (Length > 0f)
        {
            return Length;
        }

        if (Height > 0f)
        {
            return Height;
        }

        return Length;
    }
}
