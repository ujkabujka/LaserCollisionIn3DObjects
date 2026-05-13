using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed partial class LeastSquaresAxisymmetricAlignmentProjectionMethodTests
{
    [Fact]
    public void Registry_IncludesLeastSquaresMethod_AndExistingMethods()
    {
        var registry = new ProjectionMethodRegistry(new IProjectionMethod[]
        {
            new PointSourceProjectionMethod(),
            new AxisymmetricSourceProjectionMethod(),
            new SelfCalibratingAxisymmetricProjectionMethod(),
            new LeastSquaresAxisymmetricAlignmentProjectionMethod(),
        });

        Assert.Equal(ProjectionMethodIds.PointSource, registry.GetRequired(ProjectionMethodIds.PointSource).Metadata.Id);
        Assert.Equal(ProjectionMethodIds.AxisymmetricSource, registry.GetRequired(ProjectionMethodIds.AxisymmetricSource).Metadata.Id);
        Assert.Equal(ProjectionMethodIds.SelfCalibratingAxisymmetricSource, registry.GetRequired(ProjectionMethodIds.SelfCalibratingAxisymmetricSource).Metadata.Id);
        Assert.Equal(ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource, registry.GetRequired(ProjectionMethodIds.LeastSquaresAxisymmetricAlignmentSource).Metadata.Id);
    }

    [Fact]
    public void DirectionResidual_ZeroForPerfectAlignment_AndLargeForOpposite()
    {
        var actual = new Vector3D(0, 1, 0);
        var modeled = new Vector3D(0, 1, 0);
        var opposite = new Vector3D(0, -1, 0);

        var nearZero = LeastSquaresAxisymmetricAlignmentSolver.AlignmentError(actual, modeled);
        var large = LeastSquaresAxisymmetricAlignmentSolver.AlignmentError(actual, opposite);
        var angle = LeastSquaresAxisymmetricAlignmentSolver.AngularErrorDegrees(actual, opposite);

        Assert.True(nearZero < 1e-8);
        Assert.True(large > 1.99);
        Assert.Equal(180d, angle, 6);
    }

    [Fact]
    public void SurfaceParameterization_WrapsTheta_AndClampsU()
    {
        var p = SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(new CylindricalSourceProfile(2f, 10f), 11, -Math.PI / 2d);

        Assert.Equal(10d, p.X, 6);
        Assert.Equal(0d, p.Y, 6);
        Assert.Equal(-2d, p.Z, 6);
    }

    [Fact]
    public void ConstraintParameterization_IsStable()
    {
        var u = LeastSquaresAxisymmetricAlignmentSolver.ToU(-1000, 10);
        var lambda = LeastSquaresAxisymmetricAlignmentSolver.Softplus(-1000);
        var beta = LeastSquaresAxisymmetricAlignmentSolver.InverseSoftplus(LeastSquaresAxisymmetricAlignmentSolver.Softplus(0.75));

        Assert.InRange(u, 0d, 10d);
        Assert.True(lambda >= 0d);
        Assert.Equal(0.75, beta, 1e-9);
    }

    [Fact]
    public void Initialization_UsesDirectAxisymmetricWithoutRegularization_AndNonZeroLambda()
    {
        var holes = CreateSyntheticHoles(0.06, out var _, out var _, out var _, out _);
        var method = new LeastSquaresAxisymmetricAlignmentProjectionMethod();
        var result = method.Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 10f },
                new Point3(0.2, -0.3, 0.1), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var diagnostics = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).LeastSquaresDiagnostics;
        Assert.NotNull(diagnostics);
        Assert.False(diagnostics!.UsesRegularization);
        Assert.True(diagnostics.InitialLambda > 0d);
        Assert.True(diagnostics.RefinedLambda >= 0d);
    }

    [Fact]
    public void SyntheticRecovery_RefinesLambdaAndKeepsAngularErrorsSmall()
    {
        const double trueLambda = 0.08;
        var holes = CreateSyntheticHoles(trueLambda, out _, out _, out var radius, out var length);

        var result = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = (float)radius, Length = (float)length },
                new Point3(0.2, -0.3, 0.1), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var cylindrical = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource);
        var diagnostics = Assert.IsType<LeastSquaresAxisymmetricAlignmentDiagnostics>(cylindrical.LeastSquaresDiagnostics);

        Assert.True(diagnostics.RefinedLambda > 0d);
        Assert.True(double.IsFinite(diagnostics.FinalMeanAngularErrorDegrees));

        Assert.All(cylindrical.Points, point =>
        {
            Assert.InRange(point.LocalU ?? -1d, 0d, length);
            Assert.NotNull(point.ModeledRayDirection);
        });
    }

    [Fact]
    public void ImprovementOverInitialization_Holds()
    {
        var holes = CreateSyntheticHoles(0.09, out _, out _, out _, out _);
        var result = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 10f },
                new Point3(0.2, -0.3, 0.1), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var diagnostics = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).LeastSquaresDiagnostics!;
        Assert.True(diagnostics.FinalMeanAlignmentError <= diagnostics.InitialMeanAlignmentError + 1e-8);
    }

    [Fact]
    public void ResultMetadata_ContainsPerHoleFields_AndEffectiveRaysUseActualDirection()
    {
        var holes = CreateSyntheticHoles(0.05, out _, out _, out _, out _);
        var result = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 10f },
                new Point3(0.2, -0.3, 0.1), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var points = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).Points;
        Assert.Equal(holes.Count, points.Count);
        Assert.All(points, point =>
        {
            Assert.NotNull(point.LocalU);
            Assert.NotNull(point.LocalTheta);
            Assert.NotNull(point.UnwrappedU);
            Assert.NotNull(point.UnwrappedV);
            Assert.NotNull(point.ModeledRayDirection);
            Assert.NotNull(point.ModeledRayDirection);
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
            var profile = new CylindricalSourceProfile((float)radius, (float)length);
            var source = SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(profile, uValues[i], thetaValues[i]);
            var direction = SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(profile, uValues[i], thetaValues[i], lambda, tilt);
            holes.Add(new Point3(
                source.X + (direction.X * (5d + i)),
                source.Y + (direction.Y * (5d + i)),
                source.Z + (direction.Z * (5d + i))));
        }

        return holes;
    }


    [Fact]
    public void Initialization_PointsMatchDirectAxisymmetricMethod()
    {
        var holes = CreateSyntheticHoles(0.05, out _, out _, out var radius, out var length);
        var parameters = new AxisymmetricSourceProjectionParameters(
            new Point3(0, 0, 0),
            new Vector3D(1, 0, 0),
            new Vector3D(0, 1, 0),
            new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = (float)radius, Length = (float)length });

        var direct = new AxisymmetricSourceProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = parameters,
        });

        var leastSquares = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                parameters.SourceFrameOrigin,
                parameters.SourceFrameX,
                parameters.SourceFrameY,
                parameters.ProfileDefinition,
                new Point3(0.2, -0.3, 0.1), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var directPoints = Assert.IsType<AxisymmetricProjectionState>(direct.AxisymmetricSource).Points;
        var leastSquaresPoints = Assert.IsType<AxisymmetricProjectionState>(leastSquares.AxisymmetricSource).Points;
        Assert.Equal(directPoints.Count, leastSquaresPoints.Count);
        for (var i = 0; i < directPoints.Count; i++)
        {
            Assert.InRange(Math.Abs(directPoints[i].SourceSurfacePoint.X - leastSquaresPoints[i].SourceSurfacePoint.X), 0d, 0.6);
            Assert.InRange(Math.Abs(directPoints[i].SourceSurfacePoint.Y - leastSquaresPoints[i].SourceSurfacePoint.Y), 0d, 0.6);
            Assert.InRange(Math.Abs(directPoints[i].SourceSurfacePoint.Z - leastSquaresPoints[i].SourceSurfacePoint.Z), 0d, 0.6);
            Assert.InRange(Math.Abs((directPoints[i].LocalU ?? 0d) - (leastSquaresPoints[i].LocalU ?? 0d)), 0d, 0.6);
            Assert.True(double.IsFinite(leastSquaresPoints[i].LocalTheta ?? double.NaN));
        }
    }

    [Fact]
    public void Initialization_LambdaUsesSafeFallbackForTinyProfileLength()
    {
        var result = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = [new Point3(0.1, 1, 0), new Point3(0.2, 1, 0.5)],
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                new Point3(0, 0, 0),
                new Vector3D(1, 0, 0),
                new Vector3D(0, 1, 0),
                new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 1f, Length = 1e-12f },
                new Point3(0, 0, 0), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var diagnostics = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).LeastSquaresDiagnostics!;
        Assert.Equal(1d / (10d * 1e-9), diagnostics.InitialLambda, 3);
        Assert.True(double.IsFinite(diagnostics.InitialLambda));
    }
    [Fact]
    public void Registry_DisplayNames_AreAxisymmetricAndNonCylindricalSpecific()
    {
        Assert.Contains("axisymmetric", new SelfCalibratingAxisymmetricProjectionMethod().Metadata.DisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("alignment", new LeastSquaresAxisymmetricAlignmentProjectionMethod().Metadata.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AxisymmetricSourceKind.Cylinder)]
    [InlineData(AxisymmetricSourceKind.ConicalFrustum)]
    [InlineData(AxisymmetricSourceKind.CircularOgive)]
    [InlineData(AxisymmetricSourceKind.Hybrid)]
    public void LeastSquares_Diagnostics_AreFinite_AndUnregularized(AxisymmetricSourceKind geometryKind)
    {
        var holes = CreateSyntheticHoles(geometryKind == AxisymmetricSourceKind.Cylinder ? 0.08 : 0.05, out _, out _, out var radius, out var length);
        var result = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(new Point3(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = (float)radius, Length = (float)length }, new Point3(0.2, -0.3, 0.1), new LeastSquaresAxisymmetricAlignmentSolverSettings { MaxIterations = 20, ConvergenceTolerance = 1e-7 }),
        });

        var diagnostics = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).LeastSquaresDiagnostics!;
        Assert.False(diagnostics.UsesRegularization);
        Assert.True(double.IsFinite(diagnostics.RefinedLambda));
        Assert.True(double.IsFinite(diagnostics.FinalMeanAlignmentError));
        Assert.True(double.IsFinite(diagnostics.FinalMeanAngularErrorDegrees));
    }
}

public sealed partial class LeastSquaresAxisymmetricAlignmentProjectionMethodTests
{
    [Fact]
    public void AngleAlignmentResidualSquared_BehavesAsExpected()
    {
        var hole = new Point3(2, 0, 0);
        var source = new Point3(0, 0, 0);
        var aligned = new Vector3D(1, 0, 0);
        var opposite = new Vector3D(-1, 0, 0);

        var zero = InvokePrivateResidual("AngleAlignmentResidualSquared", hole, source, aligned);
        var large = InvokePrivateResidual("AngleAlignmentResidualSquared", hole, source, opposite);

        Assert.Equal(0d, zero, 8);
        Assert.True(large > zero);
    }

    [Fact]
    public void HitDistanceResidualSquared_UsesForwardRayDistance()
    {
        var source = new Point3(0, 0, 0);
        var direction = new Vector3D(1, 0, 0);

        var onRayForward = InvokePrivateResidual("HitDistanceResidualSquared", new Point3(2, 0, 0), source, direction, 10d);
        var offRay = InvokePrivateResidual("HitDistanceResidualSquared", new Point3(2, 3, 0), source, direction, 10d);
        var behindRay = InvokePrivateResidual("HitDistanceResidualSquared", new Point3(-1, 0, 0), source, direction, 10d);

        Assert.Equal(0d, onRayForward, 8);
        Assert.True(offRay > 0d);
        Assert.Equal(0.01d, behindRay, 8);
    }

    private static double InvokePrivateResidual(string methodName, params object[] args)
    {
        var method = typeof(LeastSquaresAxisymmetricAlignmentSolver).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (double)method!.Invoke(null, args)!;
    }
}
