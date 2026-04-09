using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class PrismPlacementGeneratorTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void CreateCylindricalPlacements_PlacesPrismsOnCircleWithIdentityOrientation()
    {
        var placements = PrismPlacementGenerator.CreateCylindricalPlacements(12f, 8, 3f);

        Assert.Equal(8, placements.Count);

        foreach (var placement in placements)
        {
            var horizontalRadius = MathF.Sqrt((placement.Position.X * placement.Position.X) + (placement.Position.Z * placement.Position.Z));
            Assert.Equal(12f, horizontalRadius, 3);
            Assert.Equal(3f, placement.Position.Y, 3);
            AssertVectorEqual(Quaternion.Identity, placement.Orientation);
        }
    }

    [Fact]
    public void CreateCartesianPlacements_PlacesPrismsOnSquarePerimeterWithIdentityOrientation()
    {
        var placements = PrismPlacementGenerator.CreateCartesianPlacements(20f, 12, 1.5f);
        var halfLength = 10f;

        Assert.Equal(12, placements.Count);

        foreach (var placement in placements)
        {
            Assert.Equal(1.5f, placement.Position.Y, 3);
            Assert.True(
                MathF.Abs(MathF.Abs(placement.Position.X) - halfLength) < Tolerance ||
                MathF.Abs(MathF.Abs(placement.Position.Z) - halfLength) < Tolerance);
            AssertVectorEqual(Quaternion.Identity, placement.Orientation);
        }
    }

    private static void AssertVectorEqual(Quaternion expected, Quaternion actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
        Assert.Equal(expected.W, actual.W, Tolerance);
    }
}
