using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Generation;

public static class PrismPlacementGenerator
{
    public static IReadOnlyList<FramePlacement> CreateCylindricalPlacements(float radius, int count, float z = 0f)
    {
        if (radius <= 0f)
        {
            throw new ArgumentException("Radius must be greater than zero.", nameof(radius));
        }

        if (count <= 0)
        {
            throw new ArgumentException("Count must be greater than zero.", nameof(count));
        }

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
