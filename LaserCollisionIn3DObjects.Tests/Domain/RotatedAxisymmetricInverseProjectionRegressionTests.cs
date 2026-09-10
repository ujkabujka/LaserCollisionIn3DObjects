using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class RotatedAxisymmetricInverseProjectionRegressionTests
{
    private static readonly Point3 Origin = new(3.2, -1.5, 5.0);
    private static readonly Vector3D AxisX = new(0, 0, -1);
    private static readonly Vector3D AxisY = new(1, 0, 0);
    private static readonly Point3 Tilt = new(0, 0, 0);
    private static readonly AxisymmetricSourceProfileDefinition ProfileDefinition = new()
    {
        Kind = AxisymmetricSourceKind.Cylinder,
        Radius = 1f,
        Length = 10f,
    };

    [Fact]
    public void DirectInitialization_WithRotatedTranslatedFrame_ProvidesExpectedUAndTheta()
    {
        var (holes, expectedU, expectedTheta) = CreateFullCircleHoles();
        var result = ExecuteDirect(holes);
        var points = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).Points;

        for (var i = 0; i < points.Count; i++)
        {
            Assert.Equal(expectedU[i], points[i].LocalU!.Value, 5);
            Assert.True(CircularDifference(expectedTheta[i], points[i].LocalTheta!.Value) < 1e-5);
        }
    }

    [Fact]
    public void LeastSquares_ZeroIterations_StartsAtRotatedDirectProjectionValues()
    {
        var (holes, _, _) = CreateFullCircleHoles();
        var directPoints = Assert.IsType<AxisymmetricProjectionState>(ExecuteDirect(holes).AxisymmetricSource).Points;
        var leastSquaresPoints = ExecuteLeastSquares(holes, maxIterations: 0).Points;

        for (var i = 0; i < directPoints.Count; i++)
        {
            Assert.Equal(directPoints[i].LocalU, leastSquaresPoints[i].LocalU);
            Assert.True(CircularDifference(directPoints[i].LocalTheta!.Value, leastSquaresPoints[i].LocalTheta!.Value) < 1e-6);
        }
    }

    [Fact]
    public void SelfCalibrating_RotatedTranslatedFullCircle_PreservesBroadAzimuthCoverage()
    {
        var (holes, _, _) = CreateFullCircleHoles();
        var result = new SelfCalibratingAxisymmetricProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new SelfCalibratingAxisymmetricProjectionParameters(Origin, AxisX, AxisY, ProfileDefinition, Tilt),
        });
        var points = Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource).Points;

        AssertBroadFullCircle(points);
        AssertWorldPointsMatchLocalCoordinates(points, result.SourceFrame);
    }

    [Fact]
    public void LeastSquares_RotatedTranslatedFullCircle_IsMonotonicAndPreservesCoverage()
    {
        var (holes, _, _) = CreateFullCircleHoles();
        var state = ExecuteLeastSquares(holes, maxIterations: 60);
        var diagnostics = Assert.IsType<LeastSquaresAxisymmetricAlignmentDiagnostics>(state.LeastSquaresDiagnostics);

        AssertBroadFullCircle(state.Points);
        AssertWorldPointsMatchLocalCoordinates(state.Points, state.SourceFrame);
        Assert.All(state.Points, point => Assert.True(double.IsFinite(point.FitError!.Value)));

        for (var i = 1; i < diagnostics.IterationHistory.Count; i++)
        {
            Assert.True(
                diagnostics.IterationHistory[i].ObjectiveError <= diagnostics.IterationHistory[i - 1].ObjectiveError + 1e-12,
                $"Objective increased at iteration {diagnostics.IterationHistory[i].Iteration}.");
        }
    }

    [Fact]
    public void LeastSquares_FailedLineSearchPreservesCurrentStateAndConvergesEarly()
    {
        var (holes, _, _) = CreateFullCircleHoles();
        var initial = ExecuteLeastSquares(holes, maxIterations: 0);
        var stalled = ExecuteLeastSquares(holes, maxIterations: 60, zeroStepSizes: true);
        var diagnostics = stalled.LeastSquaresDiagnostics!;

        Assert.True(diagnostics.Converged);
        Assert.Equal(1, diagnostics.Iterations);
        Assert.False(Assert.Single(diagnostics.IterationHistory).Improved);
        Assert.Equal(diagnostics.InitialLambda, diagnostics.RefinedLambda);
        for (var i = 0; i < initial.Points.Count; i++)
        {
            Assert.Equal(initial.Points[i].LocalU, stalled.Points[i].LocalU);
            Assert.Equal(initial.Points[i].LocalTheta, stalled.Points[i].LocalTheta);
            Assert.Equal(initial.Points[i].FitError, stalled.Points[i].FitError);
        }
    }

    private static ProjectionComputationResult ExecuteDirect(IReadOnlyList<Point3> holes)
        => new AxisymmetricSourceProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new AxisymmetricSourceProjectionParameters(Origin, AxisX, AxisY, ProfileDefinition),
        });

    private static AxisymmetricProjectionState ExecuteLeastSquares(IReadOnlyList<Point3> holes, int maxIterations, bool zeroStepSizes = false)
    {
        var result = new LeastSquaresAxisymmetricAlignmentProjectionMethod().Execute(new ProjectionRequest
        {
            HolePoints = holes,
            Parameters = new LeastSquaresAxisymmetricAlignmentProjectionParameters(
                Origin, AxisX, AxisY, ProfileDefinition, Tilt,
                new LeastSquaresAxisymmetricAlignmentSolverSettings
                {
                    MaxIterations = maxIterations,
                    ConvergenceTolerance = 1e-6,
                    PointStepScale = zeroStepSizes ? 0d : 0.01d,
                    ThetaStepScale = zeroStepSizes ? 0d : 0.05d,
                    LambdaStepScale = zeroStepSizes ? 0d : 0.005d,
                }),
        });
        return Assert.IsType<AxisymmetricProjectionState>(result.AxisymmetricSource);
    }

    private static (List<Point3> Holes, double[] U, double[] Theta) CreateFullCircleHoles()
    {
        var frame = PointSourceFrameBuilder.Build(Origin, AxisX, AxisY);
        var profile = ProfileDefinition.BuildProfile();
        var theta = Enumerable.Range(0, 12).Select(i => i * Math.PI / 6d).ToArray();
        var u = Enumerable.Range(0, 12).Select(i => i * 10d / 11d).ToArray();
        var holes = new List<Point3>(theta.Length);

        for (var i = 0; i < theta.Length; i++)
        {
            var sourceLocal = SelfCalibratingAxisymmetricProjectionSolver.ParameterizeSurface(profile, u[i], theta[i]);
            var modeledLocalDirection = SelfCalibratingAxisymmetricProjectionSolver.BuildModeledDirection(profile, u[i], theta[i], 0d, Tilt);
            var holeLocal = new Point3(
                sourceLocal.X + (6d * modeledLocalDirection.X),
                sourceLocal.Y + (6d * modeledLocalDirection.Y),
                sourceLocal.Z + (6d * modeledLocalDirection.Z));
            holes.Add(PointSourceFrameTransforms.LocalToWorld(holeLocal, frame));
        }

        return (holes, u, theta);
    }

    private static void AssertBroadFullCircle(IReadOnlyList<AxisymmetricProjectionPoint> points)
    {
        var angles = points.Select(point => point.LocalTheta!.Value).OrderBy(angle => angle).ToArray();
        Assert.All(angles, angle => Assert.True(double.IsFinite(angle)));
        Assert.Equal(4, angles.Select(angle => (int)Math.Floor((angle % (2d * Math.PI)) / (Math.PI / 2d))).Distinct().Count());

        var gaps = angles.Zip(angles.Skip(1), (left, right) => right - left).Append((angles[0] + (2d * Math.PI)) - angles[^1]);
        Assert.True(gaps.Max() < Math.PI, "Recovered azimuths collapsed into one or two narrow regions.");
    }

    private static void AssertWorldPointsMatchLocalCoordinates(IReadOnlyList<AxisymmetricProjectionPoint> points, PointSourceFrameState frame)
    {
        foreach (var point in points)
        {
            var local = PointSourceFrameTransforms.WorldToLocal(point.SourceSurfacePoint, frame);
            Assert.InRange(Math.Abs(point.LocalU!.Value - local.X), 0d, 1e-5);
            Assert.True(CircularDifference(point.LocalTheta!.Value, Math.Atan2(local.Z, local.Y)) < 1e-5);
        }
    }

    private static double CircularDifference(double left, double right)
    {
        var difference = Math.Abs(left - right) % (2d * Math.PI);
        return Math.Min(difference, (2d * Math.PI) - difference);
    }
}
