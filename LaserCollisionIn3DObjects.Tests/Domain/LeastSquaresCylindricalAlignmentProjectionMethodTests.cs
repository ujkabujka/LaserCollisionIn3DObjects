using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class LeastSquaresCylindricalAlignmentProjectionMethodTests
{
    [Fact]
    public void Registry_IncludesLeastSquaresMethod_AndExistingMethods()
    {
        var registry = new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new CylindricalSourceProjectionMethod(),
            new SelfCalibratingCylindricalProjectionMethod(),
            new LeastSquaresCylindricalAlignmentProjectionMethod(),
        });

        Assert.Equal(ProjectionMethodIds.PointSource, registry.GetRequired(ProjectionMethodIds.PointSource).Metadata.Id);
        Assert.Equal(ProjectionMethodIds.CylindricalSource, registry.GetRequired(ProjectionMethodIds.CylindricalSource).Metadata.Id);
        Assert.Equal(ProjectionMethodIds.SelfCalibratingCylindricalSource, registry.GetRequired(ProjectionMethodIds.SelfCalibratingCylindricalSource).Metadata.Id);
        Assert.Equal(ProjectionMethodIds.LeastSquaresCylindricalAlignmentSource, registry.GetRequired(ProjectionMethodIds.LeastSquaresCylindricalAlignmentSource).Metadata.Id);
    }

    [Fact]
    public void DirectionResidual_ZeroForPerfectAlignment_AndLargeForOpposite()
    {
        var actual = new Vector3D(0, 1, 0);
        var modeled = new Vector3D(0, 1, 0);
        var opposite = new Vector3D(0, -1, 0);

        var nearZero = LeastSquaresCylindricalAlignmentSolver.AlignmentError(actual, modeled);
        var large = LeastSquaresCylindricalAlignmentSolver.AlignmentError(actual, opposite);
        var angle = LeastSquaresCylindricalAlignmentSolver.AngularErrorDegrees(actual, opposite);

        Assert.True(nearZero < 1e-8);
        Assert.True(large > 1.99);
        Assert.Equal(180d, angle, 6);
    }

    [Fact]
    public void SurfaceParameterization_WrapsTheta_AndClampsU()
    {
        var p = LeastSquaresCylindricalAlignmentSolver.ParameterizeSurface(11, -Math.PI / 2d, 2, 10);

        Assert.Equal(10d, p.X, 6);
        Assert.Equal(0d, p.Y, 6);
        Assert.Equal(-2d, p.Z, 6);
    }

    [Fact]
    public void ConstraintParameterization_IsStable()
    {
        var u = LeastSquaresCylindricalAlignmentSolver.ToU(-1000, 10);
        var lambda = LeastSquaresCylindricalAlignmentSolver.Softplus(-1000);
        var beta = LeastSquaresCylindricalAlignmentSolver.InverseSoftplus(LeastSquaresCylindricalAlignmentSolver.Softplus(0.75));

        Assert.InRange(u, 0d, 10d);
        Assert.True(lambda >= 0d);
        Assert.Equal(0.75, beta, 1e-9);
    }

    [Fact]
    public void Initialization_UsesSelfCalibratingWithoutRegularization()
    {
        var holes = CreateSyntheticHoles(0.06, out var _, out var _, out var _, out _);
        var method = new LeastSquaresCylindricalAlignmentProjectionMethod();
        var result = method.Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresCylindricalAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                1,
                10,
                new Point3(0.2, -0.3, 0.1)),
        });

        var diagnostics = Assert.IsType<CylindricalProjectionState>(result.CylindricalSource).LeastSquaresDiagnostics;
        Assert.NotNull(diagnostics);
        Assert.False(diagnostics!.UsesRegularization);
    }

    [Fact]
    public void SyntheticRecovery_RefinesLambdaAndKeepsAngularErrorsSmall()
    {
        const double trueLambda = 0.08;
        var holes = CreateSyntheticHoles(trueLambda, out _, out _, out var radius, out var length);

        var result = new LeastSquaresCylindricalAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresCylindricalAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                radius,
                length,
                new Point3(0.2, -0.3, 0.1)),
        });

        var cylindrical = Assert.IsType<CylindricalProjectionState>(result.CylindricalSource);
        var diagnostics = Assert.IsType<LeastSquaresCylindricalAlignmentDiagnostics>(cylindrical.LeastSquaresDiagnostics);

        Assert.InRange(diagnostics.RefinedLambda, trueLambda - 0.08, trueLambda + 0.08);
        Assert.True(diagnostics.FinalMeanAngularErrorDegrees < 1.0);

        Assert.All(cylindrical.Points, point =>
        {
            Assert.InRange(point.LocalU ?? -1d, 0d, length);
            Assert.NotNull(point.AngularErrorDegrees);
            Assert.NotNull(point.AlignmentError);
        });
    }

    [Fact]
    public void ImprovementOverInitialization_Holds()
    {
        var holes = CreateSyntheticHoles(0.09, out _, out _, out _, out _);
        var result = new LeastSquaresCylindricalAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresCylindricalAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                1,
                10,
                new Point3(0.2, -0.3, 0.1)),
        });

        var diagnostics = Assert.IsType<CylindricalProjectionState>(result.CylindricalSource).LeastSquaresDiagnostics!;
        Assert.True(diagnostics.FinalMeanAlignmentError <= diagnostics.InitialMeanAlignmentError + 1e-8);
    }

    [Fact]
    public void ResultMetadata_ContainsPerHoleFields_AndEffectiveRaysUseActualDirection()
    {
        var holes = CreateSyntheticHoles(0.05, out _, out _, out _, out _);
        var result = new LeastSquaresCylindricalAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresCylindricalAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                1,
                10,
                new Point3(0.2, -0.3, 0.1)),
        });

        var points = Assert.IsType<CylindricalProjectionState>(result.CylindricalSource).Points;
        Assert.Equal(holes.Count, points.Count);
        Assert.All(points, point =>
        {
            Assert.NotNull(point.LocalU);
            Assert.NotNull(point.LocalTheta);
            Assert.NotNull(point.UnwrappedU);
            Assert.NotNull(point.UnwrappedV);
            Assert.NotNull(point.ModeledRayDirection);
            Assert.NotNull(point.AlignmentError);
            Assert.NotNull(point.AngularErrorDegrees);
        });

        var effectiveRays = result.GetEffectiveRays();
        Assert.Equal(points.Count, effectiveRays.Count);
        Assert.Equal(points[0].RayOrigin.X, effectiveRays[0].Ray.Origin.X, 6);
        Assert.Equal(points[0].RayDirection.X, effectiveRays[0].Ray.Direction.X, 6);
    }

    private static List<Point3> CreateSyntheticHoles(
        double lambda,
        out double[] uValues,
        out double[] thetaValues,
        out double radius,
        out double length)
    {
        radius = 1d;
        length = 10d;
        var tilt = new Point3(0.2, -0.3, 0.1);
        uValues = [1.2, 3.8, 6.1, 8.5, 2.4, 7.3];
        thetaValues = [0.2, 1.1, 2.2, 3.1, 4.4, 5.2];

        var holes = new List<Point3>(uValues.Length);
        for (var i = 0; i < uValues.Length; i++)
        {
            var source = LeastSquaresCylindricalAlignmentSolver.ParameterizeSurface(uValues[i], thetaValues[i], radius, length);
            var direction = LeastSquaresCylindricalAlignmentSolver.BuildModeledDirection(uValues[i], thetaValues[i], lambda, radius, tilt);
            holes.Add(new Point3(
                source.X + (direction.X * (5d + i)),
                source.Y + (direction.Y * (5d + i)),
                source.Z + (direction.Z * (5d + i))));
        }

        return holes;
    }
}
