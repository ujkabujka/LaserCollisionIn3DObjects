namespace LaserCollisionIn3DObjects.Domain.Geometry;

public sealed record HybridAxisymmetricSourceSegmentDefinition(
    HybridAxisymmetricSourceSegmentKind Kind,
    float Length,
    float RadiusStart,
    float RadiusEnd,
    float? ArcRadius = null,
    OgiveCurvatureDirection OgiveCurvatureDirection = OgiveCurvatureDirection.Outward);
