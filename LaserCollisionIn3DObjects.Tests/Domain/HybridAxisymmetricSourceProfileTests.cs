using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class HybridAxisymmetricSourceProfileTests
{
    [Fact]
    public void Construct_WithMultipleSegments_ComputesTotalLength()
    {
        var profile = new HybridAxisymmetricSourceProfile(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 2f, 3f, 3f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 4f, 3f, 5f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.CircularOgive, 3f, 5f, 4f, 12f),
        });

        Assert.Equal(9f, profile.Length, 3);
        Assert.Equal(3, profile.Segments.Count);
    }

    [Fact]
    public void Construct_RejectsEmptySegments() =>
        Assert.Throws<ArgumentException>(() => new HybridAxisymmetricSourceProfile(Array.Empty<HybridAxisymmetricSourceSegmentDefinition>()));

    [Fact]
    public void Construct_RejectsDiscontinuity()
    {
        var ex = Assert.Throws<ArgumentException>(() => new HybridAxisymmetricSourceProfile(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 2f, 3f, 3f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 2f, 2.5f, 5f),
        }));

        Assert.Contains("Segment continuity failed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Radius_AndDerivative_DelegateToActiveSegment()
    {
        var profile = new HybridAxisymmetricSourceProfile(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 2f, 3f, 3f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 4f, 3f, 5f),
        });

        Assert.Equal(3f, profile.RadiusAt(0f), 3);
        Assert.Equal(5f, profile.RadiusAt(6f), 3);
        Assert.Equal(0f, profile.RadiusDerivativeAt(1f), 3);
        Assert.Equal((5f - 3f) / 4f, profile.RadiusDerivativeAt(3f), 3);
    }

    [Fact]
    public void SurfacePoint_AndBaseDirection_AreConsistent()
    {
        var profile = new HybridAxisymmetricSourceProfile(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 4f, 2f, 4f),
        });

        var point = profile.EvaluateSurfacePoint(2f, MathF.PI / 2f);
        Assert.Equal(2f, point.X, 3);
        Assert.Equal(0f, point.Y, 3);
        Assert.Equal(3f, point.Z, 3);

        var direction = profile.EvaluateBaseDirection(2f, 0f);
        Assert.Equal(1f, direction.Length(), 3);
        Assert.True(direction.X < 0f);
    }

    [Fact]
    public void AxisymmetricRayGenerator_WithHybridProfile_UsesBlendFormula()
    {
        var profile = new HybridAxisymmetricSourceProfile(new[]
        {
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 2f, 3f, 3f),
            new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 2f, 3f, 4f),
        });
        var source = new AxisymmetricLightSource("Hybrid", new Frame3D(), AxisymmetricSourceKind.Hybrid, profile, 25, 0.2f, new Vector3(1f, 0f, 0f));
        var generator = new AxisymmetricRayGenerator();

        var rays = generator.Generate(source);
        Assert.Equal(25, rays.Count);

        foreach (var ray in rays)
        {
            var localOrigin = source.Frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(source.Frame.TransformDirectionToLocal(ray.Direction));
            var theta = MathF.Atan2(localOrigin.Z, localOrigin.Y);
            var baseDirection = profile.EvaluateBaseDirection(localOrigin.X, theta);
            var expected = Vector3.Normalize(baseDirection + (source.TiltWeight * (localOrigin - source.TiltPointLocal)));

            Assert.Equal(1f, localDirection.Length(), 3);
            Assert.Equal(expected.X, localDirection.X, 3);
            Assert.Equal(expected.Y, localDirection.Y, 3);
            Assert.Equal(expected.Z, localDirection.Z, 3);
        }
    }
}
