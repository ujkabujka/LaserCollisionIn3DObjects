using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Rays;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class SmokeTests
{
    [Fact]
    public void Ray_CanBeCreated()
    {
        var ray = new Ray(new Point3(0, 0, 0), new Vector3D(1, 0, 0));

        Assert.Equal(0, ray.Origin.X);
        Assert.Equal(1, ray.Direction.X);
    }
}
