using System.Numerics;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using LaserCollisionIn3DObjects.Domain.Scene;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public class PointSourceProjectionMethodTests
{
    private readonly PointSourceProjectionMethod _method = new();

    [Fact]
    public void Execute_CreatesOneRayPerHole()
    {
        var request = BuildRequest(new Point3(1, 2, 3), new Point3(2, 2, 3), new Point3(1, 4, 5));

        var result = _method.Execute(request);

        Assert.Equal(2, result.Rays.Count);
    }

    [Fact]
    public void Execute_UsesSourcePointAsRayOriginForAllRays()
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
        var holes = new[]
        {
            new Point3(3, 1, 1),
            new Point3(1, 4, 5),
        };

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
    public void Execute_ThrowsForEmptyHoleList()
    {
        var request = BuildRequest(new Point3(0, 0, 0));

        var exception = Assert.Throws<ArgumentException>(() => _method.Execute(request));

        Assert.Contains("at least one hole point", exception.Message);
    }

    [Fact]
    public void Execute_ThrowsForNullRequest()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _method.Execute(null!));

        Assert.Equal("request", exception.ParamName);
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
    public void ProjectionWorkspace_DefaultMethodIsPointSource()
    {
        Assert.Equal(ProjectionMethodIds.PointSource, ProjectionWorkspaceState.DefaultMethodId);
    }

    [Fact]
    public void SceneProjectionStateUpdater_PersistsProjectionResult()
    {
        var sceneProjectionState = new SceneProjectionState();
        var result = _method.Execute(BuildRequest(new Point3(0, 0, 0), new Point3(1, 0, 0)));

        SceneProjectionStateUpdater.Apply(sceneProjectionState, result);

        Assert.Equal(ProjectionMethodIds.PointSource, sceneProjectionState.SelectedMethodId);
        Assert.Same(result, sceneProjectionState.LastResult);
        Assert.NotNull(sceneProjectionState.LastResult?.PointLightSource);
        Assert.Single(sceneProjectionState.LastResult!.Rays);
    }

    private static ProjectionRequest BuildRequest(Point3 source, params Point3[] holes)
    {
        return new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new PointSourceProjectionParameters(source),
        };
    }
}
