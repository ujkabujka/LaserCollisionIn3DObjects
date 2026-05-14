using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Geometry;

/// <summary>
/// Piecewise axisymmetric profile with C0 radius continuity across segments.
/// Derivative/base direction may jump at segment boundaries.
/// </summary>
public sealed class HybridAxisymmetricSourceProfile : IAxisymmetricSourceProfile
{
    public HybridAxisymmetricSourceProfile(IReadOnlyList<HybridAxisymmetricSourceSegmentDefinition> definitions)
    {
        if (definitions is null || definitions.Count == 0)
        {
            throw new ArgumentException("Hybrid source requires at least one segment.", nameof(definitions));
        }

        var segments = new List<HybridAxisymmetricSourceSegment>(definitions.Count);
        var startU = 0f;

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            ValidateDefinition(definition, i);

            if (i > 0)
            {
                var expectedStart = definitions[i - 1].RadiusEnd;
                if (MathF.Abs(definition.RadiusStart - expectedStart) > 1e-4f)
                {
                    throw new ArgumentException($"Segment continuity failed: Segment {i + 1} R1 must equal Segment {i} R2.", nameof(definitions));
                }
            }

            var profile = CreateSegmentProfile(definition, i);
            segments.Add(new HybridAxisymmetricSourceSegment(definition, startU, profile));
            startU += profile.Length;
        }

        if (startU <= 0f)
        {
            throw new ArgumentException("Hybrid source total length must be greater than zero.", nameof(definitions));
        }

        Segments = segments;
        Length = startU;
    }

    public IReadOnlyList<HybridAxisymmetricSourceSegment> Segments { get; }

    public float Length { get; }

    public float RadiusAt(float u)
    {
        var (segment, localU) = ResolveSegment(u);
        return segment.Profile.RadiusAt(localU);
    }

    public float RadiusDerivativeAt(float u)
    {
        var (segment, localU) = ResolveSegment(u);
        return segment.Profile.RadiusDerivativeAt(localU);
    }

    public Vector3 EvaluateSurfacePoint(float u, float theta)
    {
        var radius = RadiusAt(u);
        return new Vector3(u, radius * MathF.Cos(theta), radius * MathF.Sin(theta));
    }

    public Vector3 EvaluateBaseDirection(float u, float theta)
    {
        var derivative = RadiusDerivativeAt(u);
        return Vector3.Normalize(new Vector3(-derivative, MathF.Cos(theta), MathF.Sin(theta)));
    }

    private (HybridAxisymmetricSourceSegment Segment, float LocalU) ResolveSegment(float u)
    {
        AxisymmetricSourceProfile.EnsureUInRange(u, Length);

        if (u == 0f)
        {
            var first = Segments[0];
            return (first, 0f);
        }

        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            var isLast = i == Segments.Count - 1;
            if (u < segment.EndU || isLast)
            {
                return (segment, Math.Clamp(u - segment.StartU, 0f, segment.Profile.Length));
            }

            if (MathF.Abs(u - segment.EndU) <= 1e-6f)
            {
                return (segment, segment.Profile.Length);
            }
        }

        throw new InvalidOperationException("Unable to resolve hybrid segment.");
    }

    private static void ValidateDefinition(HybridAxisymmetricSourceSegmentDefinition definition, int index)
    {
        var segmentNumber = index + 1;
        if (definition.Length <= 0f)
        {
            throw new ArgumentException($"Segment {segmentNumber} length must be greater than zero.", nameof(definition));
        }

        if (definition.RadiusStart <= 0f)
        {
            throw new ArgumentException($"Segment {segmentNumber} R1 must be greater than zero.", nameof(definition));
        }

        if (definition.RadiusEnd <= 0f)
        {
            throw new ArgumentException($"Segment {segmentNumber} R2 must be greater than zero.", nameof(definition));
        }

        if (definition.Kind == HybridAxisymmetricSourceSegmentKind.CircularOgive && (definition.ArcRadius is null || definition.ArcRadius <= 0f))
        {
            throw new ArgumentException($"Segment {segmentNumber} ogive arc radius must be greater than zero.", nameof(definition));
        }
    }

    private static IAxisymmetricSourceProfile CreateSegmentProfile(HybridAxisymmetricSourceSegmentDefinition definition, int index)
    {
        try
        {
            return definition.Kind switch
            {
                HybridAxisymmetricSourceSegmentKind.Cylinder => new AxisymmetricSourceProfile(definition.RadiusStart, definition.Length),
                HybridAxisymmetricSourceSegmentKind.ConicalFrustum => new ConicalFrustumSourceProfile(definition.RadiusStart, definition.RadiusEnd, definition.Length),
                HybridAxisymmetricSourceSegmentKind.CircularOgive => new CircularOgiveSourceProfile(definition.RadiusStart, definition.RadiusEnd, definition.Length, definition.ArcRadius!.Value, definition.OgiveCurvatureDirection),
                _ => throw new ArgumentOutOfRangeException(nameof(definition.Kind), definition.Kind, "Unsupported hybrid segment kind."),
            };
        }
        catch (ArgumentException ex) when (definition.Kind == HybridAxisymmetricSourceSegmentKind.CircularOgive)
        {
            throw new ArgumentException($"Segment {index + 1} {ex.Message}", ex);
        }
    }
}
