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

    [Theory]
    [MemberData(nameof(Orientations))]
    public void CreateBestFit_PerfectRectangle_RecoversEveryRotation(float x, float y, float z)
    {
        var rotation = FrameOrientationBuilder.ApplyLocalEulerDegrees(Quaternion.Identity, x, y, z);
        var width = Vector3.Transform(Vector3.UnitY, rotation);
        var down = Vector3.Transform(-Vector3.UnitZ, rotation);
        var lt = new Vector3(2, -3, 4);
        var frame = MeasuredPanelFrameBuilder.CreateBestFit(lt, lt + width * 2, lt + width * 2 + down * 3, lt + down * 3, 2, 3);

        AssertDirection(width, frame.Width);
        AssertDirection(down, frame.Down);
        Assert.NotNull(frame.Residuals);
        Assert.InRange(frame.Residuals!.Value.Rmse, 0, 1e-5f);
        AssertDirection(Vector3.Transform(Vector3.UnitY, rotation), Vector3.Transform(Vector3.UnitY, frame.Orientation));
    }

    [Fact]
    public void CreateBestFit_NoisyCorners_UsesEveryNonAnchorAndImprovesObjective()
    {
        var lt = Vector3.Zero;
        var rt = new Vector3(2, .15f, .05f);
        var lb = new Vector3(-.2f, 3, .15f);
        var rb = new Vector3(2.15f, 3.2f, -.2f);
        var oldFrame = MeasuredPanelFrameBuilder.Create(lt, rt, lb);
        var fit = MeasuredPanelFrameBuilder.CreateBestFit(lt, rt, rb, lb, 2, 3);

        Assert.True(Vector3.Distance(oldFrame.Width, fit.Width) > 1e-3f);
        Assert.True(Error(fit) <= Error(oldFrame) + 1e-5f);
        Assert.InRange(MathF.Abs(Vector3.Dot(fit.Width, fit.Down)), 0, 1e-5f);
        AssertDirection(fit.Normal, Vector3.Cross(fit.Width, fit.Down));

        float Error(MeasuredPanelFrame frame)
            => Vector3.DistanceSquared(rt, frame.Width * 2) + Vector3.DistanceSquared(lb, frame.Down * 3)
               + Vector3.DistanceSquared(rb, frame.Width * 2 + frame.Down * 3);
    }

    [Fact]
    public void CreateBestFit_RejectsDegenerateOrNonFiniteMeasurements()
    {
        Assert.Throws<ArgumentException>(() => MeasuredPanelFrameBuilder.CreateBestFit(Vector3.Zero, Vector3.Zero, Vector3.One, Vector3.UnitY, 1, 1));
        Assert.Throws<ArgumentException>(() => MeasuredPanelFrameBuilder.CreateBestFit(Vector3.Zero, Vector3.UnitX, Vector3.One, new Vector3(float.NaN), 1, 1));
    }

    private static void AssertDirection(Vector3 expected, Vector3 actual)
        => Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"Expected {expected}, actual {actual}.");
}
