using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CylindricalRayGeneratorTests
{
    [Fact]
    public void Generate_ReturnsConfiguredRayCount()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 2f, 4f, 150, Vector3.UnitX);

        var rays = generator.Generate(source);

        Assert.Equal(150, rays.Count);
    }

    [Fact]
    public void Generate_RayOriginsStayOnCylindricalShell()
    {
        var generator = new CylindricalRayGenerator();
        var source = new CylindricalLightSource("S", new Frame3D(), 3f, 6f, 40, Vector3.UnitX);

        var rays = generator.Generate(source);

        foreach (var ray in rays)
        {
            var radialDistance = MathF.Sqrt((ray.Origin.X * ray.Origin.X) + (ray.Origin.Z * ray.Origin.Z));
            Assert.Equal(3f, radialDistance, 3);
            Assert.InRange(ray.Origin.Y, -3f, 3f);
        }
    }
}
