using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class AxisymmetricSourceProfileTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void FrustumProfile_RadiusAndDerivative_AreCorrect()
    {
        var profile = new ConicalFrustumSourceProfile(2f, 6f, 8f);

        Assert.Equal(2f, profile.RadiusAt(0f), 3);
        Assert.Equal(6f, profile.RadiusAt(8f), 3);
        Assert.Equal(4f, profile.RadiusAt(4f), 3);
        Assert.Equal(0.5f, profile.RadiusDerivativeAt(1f), 3);
    }

    [Fact]
    public void FrustumProfile_WithEqualRadii_HasCylindricalBaseDirection()
    {
        var profile = new ConicalFrustumSourceProfile(4f, 4f, 5f);
        var direction = profile.EvaluateBaseDirection(2f, 1.2f);

        Assert.Equal(0f, direction.X, Tolerance);
        Assert.Equal(1f, direction.Length(), Tolerance);
    }

    [Fact]
    public void FrustumProfile_IncreasingRadius_HasNegativeAxialNormalComponent()
    {
        var profile = new ConicalFrustumSourceProfile(2f, 4f, 6f);
        var direction = profile.EvaluateBaseDirection(3f, 0.5f);

        Assert.True(direction.X < 0f);
        Assert.Equal(1f, direction.Length(), Tolerance);
    }

    [Fact]
    public void OgiveProfile_RejectsTooSmallArcRadius()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CircularOgiveSourceProfile(2f, 5f, 10f, 4f, OgiveCurvatureDirection.Outward));
        Assert.Contains("Ogive arc radius is too small", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OgiveProfile_MatchesEndpoints()
    {
        var profile = new CircularOgiveSourceProfile(2f, 3f, 2f, 20f, OgiveCurvatureDirection.Outward);
        Assert.Equal(2f, profile.RadiusAt(0f), 3);
        Assert.Equal(3f, profile.RadiusAt(2f), 3);
    }

    [Fact]
    public void OgiveProfile_OutwardAndInward_MidpointConvention()
    {
        var outward = new CircularOgiveSourceProfile(2f, 3f, 2f, 20f, OgiveCurvatureDirection.Outward);
        var inward = new CircularOgiveSourceProfile(2f, 3f, 2f, 20f, OgiveCurvatureDirection.Inward);
        var linearMid = 2.5f;

        Assert.True(outward.RadiusAt(1f) > linearMid);
        Assert.True(inward.RadiusAt(1f) < linearMid);
    }

    [Theory]
    [InlineData(2f, 3f)]
    [InlineData(3f, 2f)]
    public void OgiveProfile_IsMonotonic(float r1, float r2)
    {
        var profile = new CircularOgiveSourceProfile(r1, r2, 2f, 20f, OgiveCurvatureDirection.Outward);
        var previous = profile.RadiusAt(0f);
        for (var i = 1; i <= 100; i++)
        {
            var current = profile.RadiusAt(2f * (i / 100f));
            if (r2 > r1)
            {
                Assert.True(current + 1e-5f >= previous);
            }
            else
            {
                Assert.True(current - 1e-5f <= previous);
            }

            var derivative = profile.RadiusDerivativeAt(Math.Min(1.9f, 2f * (i / 100f)));
            Assert.False(float.IsNaN(derivative) || float.IsInfinity(derivative));
            var baseDirection = profile.EvaluateBaseDirection(Math.Min(1.9f, 2f * (i / 100f)), 0.4f);
            Assert.Equal(1f, baseDirection.Length(), 3);
            previous = current;
        }
    }

    [Fact]
    public void AxisymmetricGenerator_ProducesValidSurfaceOriginsAndDirections()
    {
        var profile = new ConicalFrustumSourceProfile(2f, 3f, 6f);
        var source = new AxisymmetricLightSource("F", new Frame3D(), AxisymmetricSourceKind.ConicalFrustum, profile, rayCount: 64, tiltWeight: 0f, tiltPointLocal: Vector3.Zero);
        var generator = new AxisymmetricRayGenerator();

        var rays = generator.Generate(source);
        Assert.Equal(64, rays.Count);

        foreach (var ray in rays)
        {
            var localOrigin = source.Frame.TransformPointToLocal(ray.Origin);
            var radius = MathF.Sqrt((localOrigin.Y * localOrigin.Y) + (localOrigin.Z * localOrigin.Z));
            Assert.Equal(profile.RadiusAt(localOrigin.X), radius, 3);

            var localDirection = Vector3.Normalize(source.Frame.TransformDirectionToLocal(ray.Direction));
            var theta = MathF.Atan2(localOrigin.Z, localOrigin.Y);
            var expectedBase = profile.EvaluateBaseDirection(localOrigin.X, theta);
            Assert.Equal(expectedBase.X, localDirection.X, 3);
            Assert.Equal(expectedBase.Y, localDirection.Y, 3);
            Assert.Equal(expectedBase.Z, localDirection.Z, 3);
            Assert.Equal(1f, localDirection.Length(), 3);
        }
    }

    [Fact]
    public void AxisymmetricGenerator_WithTilt_MatchesBlendEquation()
    {
        var profile = new ConicalFrustumSourceProfile(2f, 4f, 5f);
        var source = new AxisymmetricLightSource("F", new Frame3D(), AxisymmetricSourceKind.ConicalFrustum, profile, rayCount: 16, tiltWeight: 0.2f, tiltPointLocal: new Vector3(1f, 0f, 0f));
        var generator = new AxisymmetricRayGenerator();

        var rays = generator.Generate(source);
        foreach (var ray in rays)
        {
            var localOrigin = source.Frame.TransformPointToLocal(ray.Origin);
            var theta = MathF.Atan2(localOrigin.Z, localOrigin.Y);
            var baseDirection = profile.EvaluateBaseDirection(localOrigin.X, theta);
            var expected = Vector3.Normalize(baseDirection + (0.2f * (localOrigin - source.TiltPointLocal)));
            var actual = Vector3.Normalize(source.Frame.TransformDirectionToLocal(ray.Direction));
            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Y, actual.Y, 3);
            Assert.Equal(expected.Z, actual.Z, 3);
        }
    }

    [Theory]
    [InlineData(AxisymmetricSourceKind.Cylinder)]
    [InlineData(AxisymmetricSourceKind.ConicalFrustum)]
    [InlineData(AxisymmetricSourceKind.CircularOgive)]
    [InlineData(AxisymmetricSourceKind.Hybrid)]
    public void ProfileDefinitionBuilder_SupportsAllGeometryKinds(AxisymmetricSourceKind kind)
    {
        var profile = BuildProfile(kind);
        Assert.True(profile.Length > 0f);
        var direction = profile.EvaluateBaseDirection(profile.Length * 0.5f, 0.25f);
        Assert.Equal(1f, direction.Length(), 3);
    }

    private static IAxisymmetricSourceProfile BuildProfile(AxisymmetricSourceKind kind)
    {
        return kind switch
        {
            AxisymmetricSourceKind.Cylinder => new CylindricalSourceProfile(2f, 5f),
            AxisymmetricSourceKind.ConicalFrustum => new ConicalFrustumSourceProfile(2f, 3f, 5f),
            AxisymmetricSourceKind.CircularOgive => new CircularOgiveSourceProfile(2f, 3f, 5f, 15f, OgiveCurvatureDirection.Outward),
            AxisymmetricSourceKind.Hybrid => new HybridAxisymmetricSourceProfile([
                new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.Cylinder, 2f, 2f, 2f),
                new HybridAxisymmetricSourceSegmentDefinition(HybridAxisymmetricSourceSegmentKind.ConicalFrustum, 3f, 2f, 3f),
            ]),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
