using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Generation;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class MeasuredPanelFrameBuilderTests
{
    public static TheoryData<float, float, float> Orientations => new()
    {
        { 0, 0, 0 }, { 25, 0, 0 }, { -25, 0, 0 }, { 0, 35, 0 },
        { 0, -35, 0 }, { 0, 0, 45 }, { 0, 0, -45 }, { 21, -32, 47 },
    };

    [Theory]
    [MemberData(nameof(Orientations))]
    public void Create_ReconstructsKnownLocalEulerOrientation(float x, float y, float z)
    {
        var expected = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, x, y, z);
        var origin = new Vector3(3, -4, 5);
        var width = Vector3.Transform(Vector3.UnitY, expected);
        var down = Vector3.Transform(-Vector3.UnitZ, expected);
        var actual = MeasuredPanelFrameBuilder.Create(origin, origin + width * 7, origin + down * 11);

        AssertDirection(Vector3.Transform(Vector3.UnitX, expected), Vector3.Transform(Vector3.UnitX, actual.Orientation));
        AssertDirection(Vector3.Transform(Vector3.UnitY, expected), Vector3.Transform(Vector3.UnitY, actual.Orientation));
        AssertDirection(Vector3.Transform(Vector3.UnitZ, expected), Vector3.Transform(Vector3.UnitZ, actual.Orientation));
    }

    [Fact]
    public void Create_NoisyEdges_ReturnsFiniteOrthonormalRightHandedFrame()
    {
        var frame = MeasuredPanelFrameBuilder.Create(Vector3.Zero, new Vector3(4, .02f, -.01f), new Vector3(.03f, .04f, -3));

        Assert.InRange(MathF.Abs(frame.Width.Length() - 1), 0, 1e-6f);
        Assert.InRange(MathF.Abs(frame.Down.Length() - 1), 0, 1e-6f);
        Assert.InRange(MathF.Abs(frame.Normal.Length() - 1), 0, 1e-6f);
        Assert.InRange(MathF.Abs(Vector3.Dot(frame.Width, frame.Down)), 0, 1e-6f);
        Assert.InRange(MathF.Abs(Vector3.Dot(frame.Width, frame.Normal)), 0, 1e-6f);
        Assert.InRange(MathF.Abs(Vector3.Dot(frame.Down, frame.Normal)), 0, 1e-6f);
        AssertDirection(frame.Normal, Vector3.Cross(frame.Width, frame.Down));
        Assert.True(float.IsFinite(frame.Orientation.X) && float.IsFinite(frame.Orientation.Y)
            && float.IsFinite(frame.Orientation.Z) && float.IsFinite(frame.Orientation.W));
    }

    private static void AssertDirection(Vector3 expected, Vector3 actual)
        => Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"Expected {expected}, actual {actual}.");
}
