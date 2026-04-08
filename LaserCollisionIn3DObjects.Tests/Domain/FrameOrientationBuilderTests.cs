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
        AssertVectorEqual(new Vector3(-1f, 0f, 0f), rotatedForward);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
