using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Services;

public static class PrismGeometryConverter
{
    public static RectangularPrism CreateDomainPrism(PrismItemViewModel prism) => new(
        string.IsNullOrWhiteSpace(prism.Name) ? "Prism" : prism.Name,
        new Frame3D(
            new Vector3(prism.PositionX, prism.PositionY, prism.PositionZ),
            FrameOrientationBuilder.ApplyLocalEulerDegrees(prism.BaseOrientation, prism.RotationX, prism.RotationY, prism.RotationZ)),
        prism.SizeX, prism.SizeY, prism.SizeZ);
}
