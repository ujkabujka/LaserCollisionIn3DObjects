using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class DomainValidationTests
{
    [Fact]
    public void Ray3D_WithZeroDirection_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new Ray3D(Vector3.Zero, Vector3.Zero));
    }

    [Theory]
    [InlineData(0f, 1f, 1f)]
    [InlineData(1f, 0f, 1f)]
    [InlineData(1f, 1f, 0f)]
    [InlineData(-1f, 1f, 1f)]
    [InlineData(1f, -1f, 1f)]
    [InlineData(1f, 1f, -1f)]
    public void RectangularPrism_WithNonPositiveSize_ThrowsException(float sizeX, float sizeY, float sizeZ)
    {
        Assert.Throws<ArgumentException>(() => new RectangularPrism("Invalid", new Frame3D(), sizeX, sizeY, sizeZ));
    }
}
