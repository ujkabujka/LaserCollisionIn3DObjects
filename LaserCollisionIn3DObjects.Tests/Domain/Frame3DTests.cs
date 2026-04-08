using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class Frame3DTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void TransformPointToWorld_AndBackToLocal_ReturnsOriginalPoint()
    {
        var frame = new Frame3D(
            new Vector3(10f, -3f, 2f),
            Quaternion.CreateFromYawPitchRoll(0.2f, -0.3f, 0.4f));

        var localPoint = new Vector3(1.5f, -2f, 0.5f);

        var worldPoint = frame.TransformPointToWorld(localPoint);
        var roundTripLocalPoint = frame.TransformPointToLocal(worldPoint);

        AssertVectorEqual(localPoint, roundTripLocalPoint);
    }

    [Fact]
    public void TransformDirectionToWorld_DoesNotApplyTranslation()
    {
        var frame = new Frame3D(new Vector3(100f, 200f, 300f), Quaternion.Identity);
        var direction = new Vector3(0f, 1f, 0f);

        var transformed = frame.TransformDirectionToWorld(direction);

        AssertVectorEqual(direction, transformed);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }
}
