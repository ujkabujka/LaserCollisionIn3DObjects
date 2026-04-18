using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public class PointSourceProjectionMethodTests
{
    private readonly PointSourceProjectionMethod _method = new();

    [Fact]
    public void FrameBuilder_PreservesUserXDirectionAndBuildsRightHandedFrame()
    {
        var frame = PointSourceFrameBuilder.Build(
            new Point3(0, 0, 0),
            new Vector3D(10, 0, 0),
            new Vector3D(0, 4, 0));

        Assert.Equal(1d, frame.AxisX.X, 6);
        Assert.Equal(0d, frame.AxisX.Y, 6);
        Assert.Equal(0d, frame.AxisX.Z, 6);

        Assert.Equal(0d, Dot(frame.AxisX, frame.AxisY), 6);
        Assert.Equal(0d, Dot(frame.AxisX, frame.AxisZ), 6);
        Assert.Equal(0d, Dot(frame.AxisY, frame.AxisZ), 6);

        var derivedZ = Cross(frame.AxisX, frame.AxisY);
        Assert.Equal(derivedZ.X, frame.AxisZ.X, 6);
        Assert.Equal(derivedZ.Y, frame.AxisZ.Y, 6);
        Assert.Equal(derivedZ.Z, frame.AxisZ.Z, 6);
    }

    [Fact]
    public void FrameBuilder_RejectsDegenerateInputs()
    {
        Assert.Throws<ArgumentException>(() => PointSourceFrameBuilder.Build(new Point3(), new Vector3D(), new Vector3D(0, 1, 0)));
        Assert.Throws<ArgumentException>(() => PointSourceFrameBuilder.Build(new Point3(), new Vector3D(1, 0, 0), new Vector3D()));
        Assert.Throws<ArgumentException>(() => PointSourceFrameBuilder.Build(new Point3(), new Vector3D(1, 0, 0), new Vector3D(2, 0, 0)));
    }

    [Fact]
    public void Execute_CreatesOneRayPerHole()
    {
        var request = BuildRequest(new Point3(1, 2, 3), new Point3(2, 2, 3), new Point3(1, 4, 5));
        var result = _method.Execute(request);
        Assert.Equal(2, result.Rays.Count);
    }

    [Fact]
    public void Execute_UsesSourceOriginAsRayOriginForAllRays()
    {
        var source = new Point3(3, 4, 5);
        var request = BuildRequest(source, new Point3(5, 4, 5), new Point3(3, 6, 5));

        var result = _method.Execute(request);

        Assert.All(result.Rays, ray =>
        {
            Assert.Equal((float)source.X, ray.Ray.Origin.X);
            Assert.Equal((float)source.Y, ray.Ray.Origin.Y);
            Assert.Equal((float)source.Z, ray.Ray.Origin.Z);
        });
    }

    [Fact]
    public void Execute_DirectionsMatchNormalizedSourceToHoleVectors()
    {
        var source = new Point3(1, 1, 1);
        var holes = new[] { new Point3(3, 1, 1), new Point3(1, 4, 5) };
        var result = _method.Execute(BuildRequest(source, holes));

        for (var i = 0; i < result.Rays.Count; i++)
        {
            var expected = Vector3.Normalize(new Vector3(
                (float)(holes[i].X - source.X),
                (float)(holes[i].Y - source.Y),
                (float)(holes[i].Z - source.Z)));

            Assert.Equal(expected.X, result.Rays[i].Ray.Direction.X, precision: 6);
            Assert.Equal(expected.Y, result.Rays[i].Ray.Direction.Y, precision: 6);
            Assert.Equal(expected.Z, result.Rays[i].Ray.Direction.Z, precision: 6);
        }
    }

    [Fact]
    public void Execute_ThrowsWhenHoleCoincidesWithSource()
    {
        var source = new Point3(2, 2, 2);
        var request = BuildRequest(source, new Point3(2, 2, 2));
        var exception = Assert.Throws<ArgumentException>(() => _method.Execute(request));
        Assert.Contains("coincides", exception.Message);
    }

    [Fact]
    public void SceneProjectionStateUpdater_StoresMultipleNamedResults()
    {
        var state = new SceneProjectionState();
        var first = _method.Execute(BuildRequest(new Point3(0, 0, 0), new Point3(1, 0, 0)));
        var second = _method.Execute(BuildRequest(new Point3(0, 0, 0), new Point3(0, 1, 0)));

        SceneProjectionStateUpdater.SaveResult(state, "First", first);
        SceneProjectionStateUpdater.SaveResult(state, "Second", second);

        Assert.Equal(2, state.SavedResults.Count);
        Assert.Equal("Second", state.SavedResults.Last().DisplayName);
        Assert.NotNull(state.SelectedResult);
    }

    private static ProjectionRequest BuildRequest(Point3 source, params Point3[] holes)
    {
        return new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new PointSourceProjectionParameters(source, new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)),
        };
    }

    private static double Dot(Vector3D a, Vector3D b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(
        (a.Y * b.Z) - (a.Z * b.Y),
        (a.Z * b.X) - (a.X * b.Z),
        (a.X * b.Y) - (a.Y * b.X));
}
