namespace LaserCollisionIn3DObjects.Domain.Geometry;

public static class HybridAxisymmetricSegmentContinuity
{
    public static IReadOnlyList<HybridAxisymmetricSourceSegmentDefinition> Synchronize(IReadOnlyList<HybridAxisymmetricSourceSegmentDefinition> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0)
        {
            return Array.Empty<HybridAxisymmetricSourceSegmentDefinition>();
        }

        var output = new List<HybridAxisymmetricSourceSegmentDefinition>(segments.Count)
        {
            EnsureCylinderRadii(segments[0])
        };

        for (var i = 1; i < segments.Count; i++)
        {
            var previous = output[i - 1];
            var current = segments[i] with { RadiusStart = previous.RadiusEnd };
            output.Add(EnsureCylinderRadii(current));
        }

        return output;
    }

    private static HybridAxisymmetricSourceSegmentDefinition EnsureCylinderRadii(HybridAxisymmetricSourceSegmentDefinition segment)
    {
        if (segment.Kind != HybridAxisymmetricSourceSegmentKind.Cylinder)
        {
            return segment;
        }

        return segment with { RadiusEnd = segment.RadiusStart };
    }
}
