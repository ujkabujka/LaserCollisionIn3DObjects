using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class TransferredLightSourcePoseServiceTests
{
    [Fact]
    public void TransformRay_TranslationMovesOriginAndPreservesDirection()
    {
        var oldFrame = TransferredLightSourcePoseService.CreateFrame(Vector3.Zero, Quaternion.Identity);
        var newFrame = TransferredLightSourcePoseService.CreateFrame(new Vector3(5, -2, 3), Quaternion.Identity);
        var transformed = TransferredLightSourcePoseService.TransformRay(new Ray3D(new Vector3(1, 2, 3), Vector3.UnitX), oldFrame, newFrame);
        AssertVector(new Vector3(6, 0, 6), transformed.Origin);
        AssertVector(Vector3.UnitX, transformed.Direction);
    }

    [Fact]
    public void TransformRay_ProjectionStyleFrameUsesLocalCoordinates()
    {
        var oldFrame = new PointSourceFrameState { Origin = new Point3(0, 0, 0), AxisX = new Vector3D(0, 0, -1), AxisY = new Vector3D(1, 0, 0), AxisZ = new Vector3D(0, -1, 0) };
        var orientation = TransferredLightSourcePoseService.GetOrientation(oldFrame);
        Assert.NotEqual(Quaternion.Identity, orientation);
        var newFrame = TransferredLightSourcePoseService.CreateFrame(Vector3.Zero, Quaternion.Identity);
        var transformed = TransferredLightSourcePoseService.TransformRay(new Ray3D(new Vector3(0, 0, -2), new Vector3(0, 0, -1)), oldFrame, newFrame);
        AssertVector(new Vector3(2, 0, 0), transformed.Origin);
        AssertVector(Vector3.UnitX, transformed.Direction);
        Assert.Equal(1f, transformed.Direction.Length(), 5);
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 5); Assert.Equal(expected.Y, actual.Y, 5); Assert.Equal(expected.Z, actual.Z, 5);
    }
}
