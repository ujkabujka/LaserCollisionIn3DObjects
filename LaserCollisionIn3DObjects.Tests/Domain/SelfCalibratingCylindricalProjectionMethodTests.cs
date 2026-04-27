using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class SelfCalibratingCylindricalProjectionMethodTests
{
    [Fact]
    public void Registry_IncludesSelfCalibratingMethod()
    {
        var registry = new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new CylindricalSourceProjectionMethod(),
            new SelfCalibratingCylindricalProjectionMethod(),
        });

        var method = registry.GetRequired(ProjectionMethodIds.SelfCalibratingCylindricalSource);
        Assert.Equal(ProjectionMethodIds.SelfCalibratingCylindricalSource, method.Metadata.Id);
    }

    [Fact]
    public void Solver_FrameValidation_RejectsParallelAxes()
    {
        var method = new SelfCalibratingCylindricalProjectionMethod();
        var ex = Assert.Throws<ArgumentException>(() => method.Execute(new ProjectionRequest
        {
            HolePoints = [new Point3(1, 1, 1)],
            Parameters = new SelfCalibratingCylindricalProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(2, 0, 0),
                1,
                2,
                new Point3(0, 0, 0)),
        }));

        Assert.Contains("parallel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parameterization_WrapsTheta_AndClampsU()
    {
        var point = SelfCalibratingCylindricalProjectionSolver.ParameterizeSurface(12, -Math.PI / 2d, 2, 10);

        Assert.Equal(10d, point.X, 6);
        Assert.Equal(0d, point.Y, 6);
        Assert.Equal(-2d, point.Z, 6);
    }

    [Fact]
    public void ModeledDirection_WithZeroLambda_IsPureRadial()
    {
        var direction = SelfCalibratingCylindricalProjectionSolver.BuildModeledDirection(1, Math.PI / 3d, 0d, 2d, new Point3(0, 0, 0));

        Assert.Equal(0d, direction.X, 6);
        Assert.Equal(Math.Cos(Math.PI / 3d), direction.Y, 6);
        Assert.Equal(Math.Sin(Math.PI / 3d), direction.Z, 6);
    }

    [Fact]
    public void PointToRayError_IsNearZeroForOnRayPoint_AndTClamped()
    {
        var tilt = new Point3(0, 0, 0);
        var errOn = SelfCalibratingCylindricalProjectionSolver.PointToRayError(new Point3(2, 5, 0), 2, 0, 0, 1, tilt);
        var errBehind = SelfCalibratingCylindricalProjectionSolver.PointToRayError(new Point3(2, 0, 0), 2, 0, 0, 1, tilt);

        Assert.True(errOn < 1e-6);
        Assert.True(errBehind > errOn);
    }

    [Fact]
    public void Method_ProducesPerHoleMetadata_AndEstimatedLambda()
    {
        var frameOrigin = new Point3(0, 0, 0);
        var parameters = new SelfCalibratingCylindricalProjectionParameters(
            frameOrigin,
            new Vector3D(1, 0, 0),
            new Vector3D(0, 1, 0),
            1d,
            8d,
            new Point3(0, 0, 0));

        var holes = new List<Point3>();
        foreach (var (u, theta) in new[] { (1d, 0.1d), (3d, 1.4d), (5d, 2.1d), (7d, 4.0d) })
        {
            var source = new Point3(u, Math.Cos(theta), Math.Sin(theta));
            var direction = SelfCalibratingCylindricalProjectionSolver.BuildModeledDirection(u, theta, 0.1d, 1d, parameters.LocalTiltPoint);
            holes.Add(new Point3(source.X + (direction.X * 6d), source.Y + (direction.Y * 6d), source.Z + (direction.Z * 6d)));
        }

        var result = new SelfCalibratingCylindricalProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = parameters,
        });

        var cylindrical = Assert.IsType<CylindricalProjectionState>(result.CylindricalSource);
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
            MethodId = ProjectionMethodIds.SelfCalibratingCylindricalSource,
            SourceFrame = new PointSourceFrameState
            {
                Origin = new Point3(0, 0, 0),
                AxisX = new Vector3D(1, 0, 0),
                AxisY = new Vector3D(0, 1, 0),
                AxisZ = new Vector3D(0, 0, 1),
            },
            Rays = Array.Empty<ProjectionRay>(),
            CylindricalSource = new CylindricalProjectionState
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
                    new CylindricalProjectionPoint(new Point3(1, 0, 0), new Point3(0, 0, 0), new Vector3D(1, 0, 0), new Point3(0, 0, 0)),
                ],
            },
        };

        var rays = result.GetEffectiveRays();
        Assert.Single(rays);
    }
}
