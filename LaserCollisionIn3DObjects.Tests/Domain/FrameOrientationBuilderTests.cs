using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class FrameOrientationBuilderTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void ApplyLocalEulerDegrees_RollRotatesAroundCurrentLocalForwardAxis()
    {
        var baseOrientation = FrameOrientationBuilder.CreateFacingOriginOrientation(new Vector3(5f, 0f, 0f));
        var rotated = FrameOrientationBuilder.ApplyLocalEulerDegrees(baseOrientation, 0f, 0f, 90f);

        var baseForward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, baseOrientation));
        var rotatedForward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotated));

        AssertVectorEqual(baseForward, rotatedForward);
        AssertVectorEqual(Vector3.UnitZ, rotatedForward);
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(12f, -18f, 33f)]
    [InlineData(-37f, 41f, -15f)]
    [InlineData(60f, 10f, -25f)]
    public void ToLocalEulerDegrees_RoundTripsApplyLocalEulerDegrees(float rotationX, float rotationY, float rotationZ)
    {
        var original = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, rotationX, rotationY, rotationZ);
        var (extractedX, extractedY, extractedZ) = FrameOrientationBuilder.ToLocalEulerDegrees(original);
        var reconstructed = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, extractedX, extractedY, extractedZ);

        Assert.True(Quaternion.Dot(Quaternion.Normalize(original), Quaternion.Normalize(reconstructed)) > 0.9999f);
    }

    [Fact]
    public void ToLocalEulerDegrees_ReflectsCombinedBaseOrientationAndEulerDeltas()
    {
        var baseOrientation = FrameOrientationBuilder.CreateFacingOriginOrientation(new Vector3(8f, 8f, 0f));
        var combined = FrameOrientationBuilder.ApplyLocalEulerDegrees(baseOrientation, 0f, 0f, 0f);
        var (rotationX, rotationY, rotationZ) = FrameOrientationBuilder.ToLocalEulerDegrees(combined);
        var reconstructed = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, rotationX, rotationY, rotationZ);

        Assert.True(Quaternion.Dot(Quaternion.Normalize(combined), Quaternion.Normalize(reconstructed)) > 0.9999f);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
