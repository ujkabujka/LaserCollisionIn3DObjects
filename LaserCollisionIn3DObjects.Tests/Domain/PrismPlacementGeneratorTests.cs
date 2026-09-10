using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class PrismPlacementGeneratorTests
{
    [Fact]
    public void FullCircle_FourPanels_UsesCardinalCentersWithoutDuplicateEndpoint()
    {
        var placements = PrismPlacementGenerator.CreateCylindricalPlacements(10, 4);
        var expected = new[] { new Vector3(10, 0, 0), new Vector3(0, 10, 0), new Vector3(-10, 0, 0), new Vector3(0, -10, 0) };
        Assert.Equal(4, placements.Count);
        for (var i = 0; i < expected.Length; i++) Assert.True(Vector3.Distance(expected[i], placements[i].Position) < 1e-4f);
    }
    [Fact]
    public void AngularStep_UsesDegreeConventionAndFacesOrigin()
    {
        var placements = PrismPlacementGenerator.CreateAngularStepPlacements(10, 30, 15, 4, 2);
        Assert.Equal(4, placements.Count);
        for (var i = 0; i < 4; i++)
        {
            var angle = FrameOrientationBuilder.DegreesToRadians(30 + i * 15);
            Assert.Equal(10 * MathF.Cos(angle), placements[i].Position.X, 4);
            Assert.Equal(10 * MathF.Sin(angle), placements[i].Position.Y, 4);
            Assert.Equal(2, placements[i].Position.Z);
            var localX = Vector3.Transform(Vector3.UnitX, placements[i].Orientation);
            var inward = Vector3.Normalize(new Vector3(-placements[i].Position.X, -placements[i].Position.Y, 0));
            Assert.True(Vector3.Distance(localX, inward) < 1e-5f);
            Assert.True(Vector3.Distance(Vector3.Transform(Vector3.UnitZ, placements[i].Orientation), Vector3.UnitZ) < 1e-5f);
        }
    }

    [Theory]
    [InlineData(-60, 60)]
    [InlineData(300, 60)]
    public void AngularRange_FitsWholePanelsWithSymmetricMargins(float start, float end)
    {
        var result = PrismPlacementGenerator.CreateAngularRangePlacements(10, 1.2f, start, end);
        var footprint = FrameOrientationBuilder.RadiansToDegrees(2 * MathF.Atan(1.2f / 20));
        Assert.Equal((int)MathF.Floor(120 / footprint), result.PanelCount);
        Assert.Equal((120 - result.PanelCount * footprint) / 2, result.MarginDegrees, 4);
        Assert.Equal(result.PanelCount, result.Placements.Count);
    }

    [Fact]
    public void AngularRange_RejectsRangeTooSmallForOnePanel()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PrismPlacementGenerator.CreateAngularRangePlacements(1, 10, 0, 1));
        Assert.Contains("too narrow", exception.Message);
    }
    private const float Tolerance = 1e-4f;

    [Fact]
    public void CreateCylindricalPlacements_PlacesPrismsOnCircleWithIdentityOrientation()
    {
        var placements = PrismPlacementGenerator.CreateCylindricalPlacements(12f, 8, 3f);

        Assert.Equal(8, placements.Count);

        foreach (var placement in placements)
        {
            var horizontalRadius = MathF.Sqrt((placement.Position.X * placement.Position.X) + (placement.Position.Y * placement.Position.Y));
            Assert.Equal(12f, horizontalRadius, 3);
            Assert.Equal(3f, placement.Position.Z, 3);
            AssertVectorEqual(FrameOrientationBuilder.CreateFacingOriginOrientation(placement.Position), placement.Orientation);
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
            Assert.Equal(1.5f, placement.Position.Z, 3);
            Assert.True(
                MathF.Abs(MathF.Abs(placement.Position.X) - halfLength) < Tolerance ||
                MathF.Abs(MathF.Abs(placement.Position.Y) - halfLength) < Tolerance);
            AssertVectorEqual(FrameOrientationBuilder.CreateFacingOriginOrientation(placement.Position), placement.Orientation);
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
