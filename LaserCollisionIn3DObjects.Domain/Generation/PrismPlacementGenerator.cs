using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Generation;

public static class PrismPlacementGenerator
{
    public static IReadOnlyList<FramePlacement> CreateCylindricalPlacements(float radius, int count, float z = 0f)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
        {
            throw new ArgumentException("Radius must be greater than zero.", nameof(radius));
        }

        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        if (!float.IsFinite(z)) throw new ArgumentException("Z position must be finite.", nameof(z));

        var placements = new List<FramePlacement>(count);

        for (var i = 0; i < count; i++)
        {
            var theta = (2f * MathF.PI * i) / count;
            var position = new Vector3(
                radius * MathF.Cos(theta),
                 radius * MathF.Sin(theta),
                 z
               );

            placements.Add(new FramePlacement(position, FrameOrientationBuilder.CreateFacingOriginOrientation(position)));
        }

        return placements;
    }

    public static IReadOnlyList<FramePlacement> CreateAngularStepPlacements(
        float radius, float startAngleDegrees, float stepAngleDegrees, int count, float z = 0f)
    {
        ValidatePolar(radius, z);
        if (!float.IsFinite(startAngleDegrees)) throw new ArgumentException("Start angle must be finite.", nameof(startAngleDegrees));
        if (!float.IsFinite(stepAngleDegrees) || stepAngleDegrees <= 0f) throw new ArgumentException("Angle between panels must be finite and greater than zero.", nameof(stepAngleDegrees));
        if (count <= 0) throw new ArgumentException("Count must be greater than zero.", nameof(count));

        return CreatePolarPlacements(radius, startAngleDegrees, stepAngleDegrees, count, z);
    }

    public static PolarRangePlacementResult CreateAngularRangePlacements(
        float radius, float panelWidth, float startAngleDegrees, float endAngleDegrees, float z = 0f)
    {
        ValidatePolar(radius, z);
        if (!float.IsFinite(panelWidth) || panelWidth <= 0f) throw new ArgumentException("Panel width must be finite and greater than zero.", nameof(panelWidth));
        if (!float.IsFinite(startAngleDegrees) || !float.IsFinite(endAngleDegrees)) throw new ArgumentException("Range angles must be finite.");

        var rawDifference = endAngleDegrees - startAngleDegrees;
        if (MathF.Abs(rawDifference) >= 360f)
            throw new ArgumentException("Angular range must be greater than zero and less than 360 degrees. Use Full Circle for a complete circle.");
        var span = rawDifference % 360f;
        if (span < 0f) span += 360f;
        if (span <= 1e-5f)
            throw new ArgumentException("Angular range must be greater than zero and less than 360 degrees. Use Full Circle for a complete circle.");

        var footprintRadians = 2f * MathF.Atan(panelWidth / (2f * radius));
        var footprintDegrees = FrameOrientationBuilder.RadiansToDegrees(footprintRadians);
        var count = (int)MathF.Floor(span / footprintDegrees);
        if (count < 1) throw new ArgumentException("The selected angular range is too narrow to fit one panel at this radius and panel width.");

        var margin = (span - count * footprintDegrees) / 2f;
        var firstCenter = startAngleDegrees + margin + footprintDegrees / 2f;
        return new PolarRangePlacementResult(
            CreatePolarPlacements(radius, firstCenter, footprintDegrees, count, z),
            count, footprintDegrees, span, margin, firstCenter);
    }

    private static IReadOnlyList<FramePlacement> CreatePolarPlacements(float radius, float firstDegrees, float stepDegrees, int count, float z)
    {
        var placements = new List<FramePlacement>(count);
        for (var i = 0; i < count; i++)
        {
            var theta = FrameOrientationBuilder.DegreesToRadians(firstDegrees + i * stepDegrees);
            var position = new Vector3(radius * MathF.Cos(theta), radius * MathF.Sin(theta), z);
            placements.Add(new FramePlacement(position, FrameOrientationBuilder.CreateFacingOriginOrientation(position)));
        }
        return placements;
    }

    private static void ValidatePolar(float radius, float z)
    {
        if (!float.IsFinite(radius) || radius <= 0f) throw new ArgumentException("Radius must be finite and greater than zero.", nameof(radius));
        if (!float.IsFinite(z)) throw new ArgumentException("Z position must be finite.", nameof(z));
    }

    public static IReadOnlyList<FramePlacement> CreateCartesianPlacements(float sideLength, int count, float y = 0f)
    {
        if (sideLength <= 0f)
        {
            throw new ArgumentException("Side length must be greater than zero.", nameof(sideLength));
        }

        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

        var halfLength = sideLength * 0.5f;
        var perimeter = sideLength * 4f;
        var step = perimeter / count;
        var placements = new List<FramePlacement>(count);

        for (var i = 0; i < count; i++)
        {
            var distance = ((i + 0.5f) * step) % perimeter;
            var position = CreateSquarePerimeterPoint(distance, halfLength, sideLength, y);
            placements.Add(new FramePlacement(position, FrameOrientationBuilder.CreateFacingOriginOrientation(position)));
        }

        return placements;
    }

    private static Vector3 CreateSquarePerimeterPoint(float distance, float halfLength, float sideLength, float z)
    {
        if (distance < sideLength)
        {
            return new Vector3(-halfLength + distance, -halfLength, z );
        }

        if (distance < sideLength * 2f)
        {
            return new Vector3(halfLength, -halfLength + (distance - sideLength), z);
        }

        if (distance < sideLength * 3f)
        {
            return new Vector3(halfLength - (distance - (sideLength * 2f)), halfLength, z);
        }

        return new Vector3(-halfLength, halfLength - (distance - (sideLength * 3f)), z);
    }
}

public sealed record PolarRangePlacementResult(
    IReadOnlyList<FramePlacement> Placements,
    int PanelCount,
    float PanelAngularFootprintDegrees,
    float SweepDegrees,
    float MarginDegrees,
    float FirstPanelCenterDegrees);
