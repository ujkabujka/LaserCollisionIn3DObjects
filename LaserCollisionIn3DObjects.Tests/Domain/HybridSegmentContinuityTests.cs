using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class HybridSegmentContinuityTests
{
    [Fact]
    public void Synchronize_KeepsFirstSegmentStartAndPropagatesContinuity()
    {
        var synced = HybridAxisymmetricSegmentContinuity.Synchronize(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 3f, 2f, 4f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 3f, 10f, 5f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.CircularOgive, 3f, 11f, 6f, 20f),
        });

        Assert.Equal(2f, synced[0].RadiusStart);
        Assert.Equal(synced[0].RadiusEnd, synced[1].RadiusStart);
        Assert.Equal(synced[1].RadiusEnd, synced[2].RadiusStart);
    }

    [Fact]
    public void Synchronize_EnforcesCylinderR1EqualsR2()
    {
        var synced = HybridAxisymmetricSegmentContinuity.Synchronize(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 3f, 2f, 9f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 3f, 3f, 11f),
        });

        Assert.Equal(2f, synced[0].RadiusEnd);
        Assert.Equal(synced[0].RadiusEnd, synced[1].RadiusStart);
        Assert.Equal(synced[1].RadiusStart, synced[1].RadiusEnd);
    }
}
