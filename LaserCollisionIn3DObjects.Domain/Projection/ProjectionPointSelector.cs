using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

/// <summary>Builds the immutable semantic point snapshot supplied to projection mathematics.</summary>
public static class ProjectionPointSelector
{
    public static IReadOnlyList<Point3> BuildEffectivePoints(
        IEnumerable<Point3> holePoints,
        IEnumerable<Point3> naturalPoints,
        bool includeNaturalPoints)
    {
        ArgumentNullException.ThrowIfNull(holePoints);
        ArgumentNullException.ThrowIfNull(naturalPoints);
        return includeNaturalPoints ? holePoints.Concat(naturalPoints).ToArray() : holePoints.ToArray();
    }
}
