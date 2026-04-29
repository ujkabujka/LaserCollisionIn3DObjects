using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class SelfCalibratingAxisymmetricProjectionMethodTests
{
    [Fact]
    public void Registry_IncludesSelfCalibratingMethod()
    {
        var registry = new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new AxisymmetricSourceProjectionMethod(),
            new SelfCalibratingAxisymmetricProjectionMethod(),
        });

        var method = registry.GetRequired(ProjectionMethodIds.SelfCalibratingAxisymmetricSource);
        Assert.Equal(ProjectionMethodIds.SelfCalibratingAxisymmetricSource, method.Metadata.Id);
    }

    [Fact]
    public void Solver_FrameValidation_RejectsParallelAxes()
    {
        var method = new SelfCalibratingAxisymmetricProjectionMethod();
        var ex = Assert.Throws<ArgumentException>(() => method.Execute(new ProjectionRequest
        {
            HolePoints = [new Point3(1, 1, 1)],
            Parameters = new SelfCalibratingAxisymmetricProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(2, 0, 0),
                new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 2f },
                new Point3(0, 0, 0)),
        }));

        Assert.Contains("parallel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parameterization_WrapsTheta_AndClampsU()
    {
        var point = SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(new CylindricalSourceProfile(2f, 10f), 12, -Math.PI / 2d);

        Assert.Equal(10d, point.X, 6);
        Assert.Equal(0d, point.Y, 6);
        Assert.Equal(-2d, point.Z, 6);
    }

    [Fact]
    public void ModeledDirection_WithZeroLambda_IsPureRadial()
    {
        var direction = SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(new CylindricalSourceProfile(2f, 10f), 1, Math.PI / 3d, 0d, new Point3(0, 0, 0));

        Assert.Equal(0d, direction.X, 6);
        Assert.Equal(Math.Cos(Math.PI / 3d), direction.Y, 6);
        Assert.Equal(Math.Sin(Math.PI / 3d), direction.Z, 6);
    }

    [Fact]
    public void PointToRayError_IsNearZeroForOnRayPoint_AndTClamped()
    {
        var tilt = new Point3(0, 0, 0);
        var errOn = SelfCalibratingAxisymmetricProjectionSolver.PointToRayError(new Point3(2, 5, 0), 2, 0, 0, new CylindricalSourceProfile(1f, 8f), tilt);
        var errBehind = SelfCalibratingAxisymmetricProjectionSolver.PointToRayError(new Point3(2, 0, 0), 2, 0, 0, new CylindricalSourceProfile(1f, 8f), tilt);

        Assert.True(errOn < 1e-6);
        Assert.True(errBehind > errOn);
    }

    [Fact]
    public void Method_ProducesPerHoleMetadata_AndEstimatedLambda()
    {
        var frameOrigin = new Point3(0, 0, 0);
        var parameters = new SelfCalibratingAxisymmetricProjectionParameters(
            frameOrigin,
            new Vector3D(1, 0, 0),
            new Vector3D(0, 1, 0),
            new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 8f },
            new Point3(0, 0, 0));

        var holes = new List<Point3>();
        foreach (var (u, theta) in new[] { (1d, 0.1d), (3d, 1.4d), (5d, 2.1d), (7d, 4.0d) })
        {
            var source = new Point3(u, Math.Cos(theta), Math.Sin(theta));
            var direction = SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(new CylindricalSourceProfile(1f, 8f), u, theta, 0.1d, parameters.LocalTiltPoint);
            holes.Add(new Point3(source.X + (direction.X * 6d), source.Y + (direction.Y * 6d), source.Z + (direction.Z * 6d)));
        }

        var result = new SelfCalibratingAxisymmetricProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = parameters,
        });

        var cylindrical = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource);
        Assert.Equal(holes.Count, cylindrical.Points.Count);
        Assert.NotNull(cylindrical.EstimatedTiltWeight);
        Assert.NotNull(cylindrical.Diagnostics);
        Assert.NotEmpty(cylindrical.Diagnostics!.CandidateScores);
        Assert.All(cylindrical.Points, point =>
        {
            Assert.NotNull(point.ModeledRayDirection);
            Assert.NotNull(point.LocalU);
            Assert.NotNull(point.LocalTheta);
            Assert.NotNull(point.UnwrappedU);
            Assert.NotNull(point.UnwrappedV);
            Assert.NotNull(point.FitError);
        });
    }

    [Fact]
    public void EffectiveRays_AreProvidedForCylindricalResults()
    {
        var result = new ProjectionComputationResult
        {
            MethodId = ProjectionMethodIds.SelfCalibratingAxisymmetricSource,
            SourceFrame = new PointSourceFrameState
            {
                Origin = new Point3(0, 0, 0),
                AxisX = new Vector3D(1, 0, 0),
                AxisY = new Vector3D(0, 1, 0),
                AxisZ = new Vector3D(0, 0, 1),
            },
            Rays = Array.Empty<ProjectionRay>(),
            AxisymmetricSource = new AxisymmetricProjectionState
            {
                SourceFrame = new PointSourceFrameState
                {
                    Origin = new Point3(0, 0, 0),
                    AxisX = new Vector3D(1, 0, 0),
                    AxisY = new Vector3D(0, 1, 0),
                    AxisZ = new Vector3D(0, 0, 1),
                },
                Radius = 1,
                Length = 1,
                Points =
                [
                    new AxisymmetricProjectionPoint(new Point3(1, 0, 0), new Point3(0, 0, 0), new Vector3D(1, 0, 0), new Point3(0, 0, 0)),
                ],
            },
        };

        var rays = result.GetEffectiveRays();
        Assert.Single(rays);
    }

    [Theory]
    [InlineData(AxisymmetricSourceKind.Cylinder)]
    [InlineData(AxisymmetricSourceKind.ConicalFrustum)]
    [InlineData(AxisymmetricSourceKind.CircularOgive)]
    [InlineData(AxisymmetricSourceKind.Hybrid)]
    public void Method_ProducesFiniteFitErrorAcrossGeometries(AxisymmetricSourceKind geometryKind)
    {
        var holes = BuildAxisymmetricHoles(geometryKind);
        var result = new SelfCalibratingAxisymmetricProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new SelfCalibratingAxisymmetricProjectionParameters(new Point3(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 8f }, new Point3(0, 0, 0)),
        });

        var points = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).Points;
        Assert.All(points, point => Assert.True(double.IsFinite(point.FitError ?? double.NaN)));
    }

    private static List<Point3> BuildAxisymmetricHoles(AxisymmetricSourceKind kind)
    {
        var holes = new List<Point3>();
        foreach (var (u, theta) in new[] { (1d, 0.2d), (3d, 1.1d), (5d, 2.2d), (7d, 4.1d) })
        {
            var lambda = kind == AxisymmetricSourceKind.Cylinder ? 0.08d : 0.12d;
            var source = SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(new CylindricalSourceProfile(1f, 8f), u, theta);
            var direction = SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(new CylindricalSourceProfile(1f, 8f), u, theta, lambda, new Point3(0, 0, 0));
            holes.Add(new Point3(source.X + (direction.X * 6d), source.Y + (direction.Y * 6d), source.Z + (direction.Z * 6d)));
        }

        return holes;
    }
}
