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
        var source = new CylindricalLightSource("S", new Frame3D(), 3f, 6f, 40);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var radialDistance = MathF.Sqrt((ray.Origin.Y * ray.Origin.Y) + (ray.Origin.Z * ray.Origin.Z));
            Assert.Equal(3f, radialDistance, 3);
            Assert.InRange(ray.Origin.X, 0f, 6f);
        }
    }

    [Fact]
    public void Generate_RayDirectionsPointRadiallyOutwardInSourceLocalSpace()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(4f, -2f, 7f),
            Quaternion.CreateFromYawPitchRoll(0.4f, -0.2f, 0.3f));
        var source = new CylindricalLightSource("S", frame, 2.5f, 8f, 48);

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
    public void Generate_RayDirectionsRemainRadialUnderNonTrivialWorldTransform()
    {
        var generator = new CylindricalRayGenerator();
        var frame = new Frame3D(
            new Vector3(-11f, 8f, 2f),
            Quaternion.CreateFromYawPitchRoll(-0.9f, 0.3f, 1.1f));
        var source = new CylindricalLightSource("S", frame, 1.2f, 5f, 63);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var localOrigin = frame.TransformPointToLocal(ray.Origin);
            var localDirection = Vector3.Normalize(frame.TransformDirectionToLocal(ray.Direction));
            var radial = new Vector3(0f, localOrigin.Y, localOrigin.Z);
            var expectedLocalDirection = Vector3.Normalize(radial);

            Assert.Equal(0f, localDirection.X, Tolerance);
            AssertVectorEqual(expectedLocalDirection, localDirection);
        }
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
