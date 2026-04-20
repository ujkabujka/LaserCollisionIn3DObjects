using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CylindricalRayGeneratorTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Generate_ReturnsConfiguredRayCount()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 2f, 4f, 150);

        var rays = generator.Generate(source);

        Assert.Equal(150, rays.Count);
    }

    [Fact]
    public void Generate_RayOriginsStayOnCylindricalShell()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 3f, 6f, 40, tiltWeight: 0.1f);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var radialDistance = MathF.Sqrt((ray.Origin.Y * ray.Origin.Y) + (ray.Origin.Z * ray.Origin.Z));
            Assert.Equal(3f, radialDistance, 3);
            Assert.InRange(ray.Origin.X, 0f, 6f);
        }
    }

    [Fact]
    public void Generate_WithZeroTiltWeight_KeepsPureRadialDirectionsInSourceLocalSpace()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(4f, -2f, 7f),
            Quaternion.CreateFromYawPitchRoll(0.4f, -0.2f, 0.3f));
        var source = new CylindricalLightSource("S", frame, 2.5f, 8f, 48, tiltWeight: 0f);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(frame.TransformDirectionToLocal(ray.Direction));
            var expectedLocalDirection = Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));

            AssertVectorEqual(expectedLocalDirection, localDirection);
            Assert.Equal(0f, localDirection.X, Tolerance);
        }
    }

    [Fact]
    public void Generate_WithDefaultTiltWeight_MatchesWeightedDirectionFormula()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(-11f, 8f, 2f),
            Quaternion.CreateFromYawPitchRoll(-0.9f, 0.3f, 1.1f));
        var source = new CylindricalLightSource("S", frame, 1.2f, 5f, 63, tiltWeight: 0.1f);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(frame.TransformDirectionToLocal(ray.Direction));
            var radial = Vector3.Normalize(new Vector3(0f, localOrigin.Y, localOrigin.Z));
            var tilt = Vector3.Normalize(localOrigin);
            var expectedLocalDirection = Vector3.Normalize(radial + (0.1f * tilt));

            AssertVectorEqual(expectedLocalDirection, localDirection);
        }
    }

    [Fact]
    public void Generate_WithPositiveTiltWeight_IncreasingLocalXReducesAngleToPositiveX()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 2f, 8f, 9, tiltWeight: 0.1f);

        var rays = generator.Generate(source);
        var row1Ray = rays[0];
        var row2Ray = rays[3];
        var row3Ray = rays[6];

        var cos1 = Vector3.Dot(Vector3.Normalize(row1Ray.Direction), Vector3.UnitX);
        var cos2 = Vector3.Dot(Vector3.Normalize(row2Ray.Direction), Vector3.UnitX);
        var cos3 = Vector3.Dot(Vector3.Normalize(row3Ray.Direction), Vector3.UnitX);

        Assert.True(cos2 > cos1);
        Assert.True(cos3 > cos2);
    }

    [Fact]
    public void Generate_ChangingTiltWeightDoesNotChangeRayOrigins()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(3f, 4f, 5f),
            Quaternion.CreateFromYawPitchRoll(0.2f, 0.3f, -0.1f));

        var zeroTiltSource = new CylindricalLightSource("S", frame, 3f, 6f, 49, tiltWeight: 0f);
        var nonZeroTiltSource = new CylindricalLightSource("S", frame, 3f, 6f, 49, tiltWeight: 0.1f);

        var zeroTiltRays = generator.Generate(zeroTiltSource);
        var nonZeroTiltRays = generator.Generate(nonZeroTiltSource);

        Assert.Equal(zeroTiltRays.Count, nonZeroTiltRays.Count);
        for (var i = 0; i < zeroTiltRays.Count; i++)
        {
            AssertVectorEqual(zeroTiltRays[i].Origin, nonZeroTiltRays[i].Origin);
        }
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
