using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class CylindricalSourceProjectionMethodTests
{
    private readonly CylindricalSourceProjectionMethod _method = new();

    [Fact]
    public void Execute_BuildsFrameFromOriginXAndY()
    {
        var result = _method.Execute(BuildRequest(
            holes: [new Point3(1, 1, 0), new Point3(2, 2, 0)],
            origin: new Point3(10, 20, 30),
            axisX: new Vector3D(5, 0, 0),
            axisY: new Vector3D(0, 4, 0),
            radius: 2,
            length: 9));

        Assert.Equal(10d, result.SourceFrame.Origin.X, 6);
        Assert.Equal(20d, result.SourceFrame.Origin.Y, 6);
        Assert.Equal(30d, result.SourceFrame.Origin.Z, 6);
        Assert.Equal(1d, result.SourceFrame.AxisX.X, 6);
        Assert.Equal(0d, result.SourceFrame.AxisX.Y, 6);
        Assert.Equal(0d, result.SourceFrame.AxisX.Z, 6);
    }

    [Fact]
    public void Execute_RejectsDegenerateFrame()
    {
        var ex = Assert.Throws<ArgumentException>(() => _method.Execute(BuildRequest(
            holes: [new Point3(0, 1, 1), new Point3(2, 1, 1)],
            origin: new Point3(0, 0, 0),
            axisX: new Vector3D(1, 0, 0),
            axisY: new Vector3D(2, 0, 0),
            radius: 1,
            length: 2)));

        Assert.Contains("parallel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_NormalizesAxialCoordinatesAcrossWholeSet()
    {
        var result = _method.Execute(BuildRequest(
            holes: [new Point3(2, 2, 0), new Point3(5, 0, 2), new Point3(8, -2, 0)],
            origin: new Point3(0, 0, 0),
            axisX: new Vector3D(1, 0, 0),
            axisY: new Vector3D(0, 1, 0),
            radius: 2,
            length: 12));

        var points = result.CylindricalSource!.Points;
        Assert.Equal(0d, points[0].SourceSurfacePoint.X, 6);
        Assert.Equal(6d, points[1].SourceSurfacePoint.X, 6);
        Assert.Equal(12d, points[2].SourceSurfacePoint.X, 6);
    }

    [Fact]
    public void Execute_ThrowsWhenAllTransformedXAreEqual()
    {
        var ex = Assert.Throws<ArgumentException>(() => _method.Execute(BuildRequest(
            holes: [new Point3(1, 1, 0), new Point3(1, 2, 0)],
            origin: new Point3(0, 0, 0),
            axisX: new Vector3D(1, 0, 0),
            axisY: new Vector3D(0, 1, 0),
            radius: 1,
            length: 4)));

        Assert.Contains("local X coordinates are equal", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ProjectsUsingOnlyYZForRadialPlacement()
    {
        var result = _method.Execute(BuildRequest(
            holes: [new Point3(1, 3, 4), new Point3(5, -6, 8)],
            origin: new Point3(0, 0, 0),
            axisX: new Vector3D(1, 0, 0),
            axisY: new Vector3D(0, 1, 0),
            radius: 10,
            length: 20));

        foreach (var point in result.CylindricalSource!.Points)
        {
            var yzRadius = Math.Sqrt(
                (point.SourceSurfacePoint.Y * point.SourceSurfacePoint.Y) +
                (point.SourceSurfacePoint.Z * point.SourceSurfacePoint.Z));
            Assert.Equal(10d, yzRadius, 6);
        }
    }

    [Fact]
    public void Execute_PreservesPerHoleMappingAndDoesNotCreateProjectionRays()
    {
        var holes = new[]
        {
            new Point3(2, 2, 0),
            new Point3(2, 2, 0),
            new Point3(6, 0, 3),
        };

        var result = _method.Execute(BuildRequest(
            holes: holes,
            origin: new Point3(0, 0, 0),
            axisX: new Vector3D(1, 0, 0),
            axisY: new Vector3D(0, 1, 0),
            radius: 4,
            length: 10));

        Assert.Empty(result.Rays);
        Assert.NotNull(result.CylindricalSource);
        Assert.Equal(holes.Length, result.CylindricalSource!.Points.Count);
        Assert.Equal(holes[0], result.CylindricalSource.Points[0].HolePoint);
        Assert.Equal(holes[1], result.CylindricalSource.Points[1].HolePoint);
        Assert.Equal(4d, result.CylindricalSource.Radius, 6);
        Assert.Equal(10d, result.CylindricalSource.Length, 6);
    }

    [Fact]
    public void Registry_IncludesCylindricalMethod()
    {
        var registry = new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new CylindricalSourceProjectionMethod(),
        });

        var method = registry.GetRequired(ProjectionMethodIds.CylindricalSource);
        Assert.Equal(ProjectionMethodIds.CylindricalSource, method.Metadata.Id);
    }

    private static ProjectionRequest BuildRequest(
        IReadOnlyList<Point3> holes,
        Point3 origin,
        Vector3D axisX,
        Vector3D axisY,
        double radius,
        double length)
    {
        return new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new CylindricalSourceProjectionParameters(origin, axisX, axisY, radius, length),
        };
    }
}
