using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class PointSourceFrameTransformsTests
{
    [Fact]
    public void WorldToLocal_UsesActualRotatedBasis()
    {
        var frame = PointSourceFrameBuilder.Build(
            new Point3(0, 0, 0),
            new Vector3D(0, 0, -1),
            new Vector3D(1, 0, 0));

        var local = PointSourceFrameTransforms.WorldToLocal(new Point3(2, 3, 4), frame);

        Assert.Equal(-4d, local.X, 10);
        Assert.Equal(2d, local.Y, 10);
        Assert.Equal(-3d, local.Z, 10);
    }

    [Theory]
    [InlineData(0, 0, 0, 1, 0, 0, 0, 1, 0)]
    [InlineData(0, 0, 0, 0, 0, -1, 1, 0, 0)]
    [InlineData(3.2, -1.5, 5, 0, 0, -1, 1, 0, 0)]
    public void PointTransform_RoundTripsIdentityRotationAndTranslation(
        double ox, double oy, double oz,
        double xx, double xy, double xz,
        double yx, double yy, double yz)
    {
        var frame = PointSourceFrameBuilder.Build(
            new Point3(ox, oy, oz),
            new Vector3D(xx, xy, xz),
            new Vector3D(yx, yy, yz));
        var world = new Point3(7.25, -3.5, 11.75);

        var reconstructed = PointSourceFrameTransforms.LocalToWorld(
            PointSourceFrameTransforms.WorldToLocal(world, frame), frame);

        Assert.Equal(world.X, reconstructed.X, 9);
        Assert.Equal(world.Y, reconstructed.Y, 9);
        Assert.Equal(world.Z, reconstructed.Z, 9);
    }

    [Fact]
    public void LocalDirectionToWorld_RotatesWithoutTranslation()
    {
        var frame = PointSourceFrameBuilder.Build(
            new Point3(3.2, -1.5, 5),
            new Vector3D(0, 0, -1),
            new Vector3D(1, 0, 0));

        var world = PointSourceFrameTransforms.LocalDirectionToWorld(new Vector3D(2, 3, 4), frame);

        Assert.Equal(3d, world.X, 10);
        Assert.Equal(-4d, world.Y, 10);
        Assert.Equal(-2d, world.Z, 10);
    }
}
