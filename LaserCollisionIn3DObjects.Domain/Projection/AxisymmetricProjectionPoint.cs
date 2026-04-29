using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record AxisymmetricProjectionPoint(
    Point3 HolePoint,
    Point3 SourceSurfacePoint,
    Vector3D RayDirection,
    Point3 RayOrigin)
{
    public Vector3D? ModeledRayDirection { get; init; }
    public double? LocalU { get; init; }
    public double? LocalTheta { get; init; }
    public double? UnwrappedU { get; init; }
    public double? UnwrappedV { get; init; }
    public double? FitError { get; init; }
    public double? AlignmentError { get; init; }
    public double? AngularErrorDegrees { get; init; }
}

public sealed class AxisymmetricProjectionState
{
    public required PointSourceFrameState SourceFrame { get; init; }

    public required double Radius { get; init; }

    public required double Length { get; init; }

    public Point3? LocalTiltPoint { get; init; }

    public double? EstimatedTiltWeight { get; init; }

    public SelfCalibratingAxisymmetricProjectionDiagnostics? Diagnostics { get; init; }

    public LeastSquaresAxisymmetricAlignmentDiagnostics? LeastSquaresDiagnostics { get; init; }

    public required IReadOnlyList<AxisymmetricProjectionPoint> Points { get; init; }
}

public sealed class SelfCalibratingAxisymmetricProjectionDiagnostics
{
    public required IReadOnlyList<SelfCalibratingAxisymmetricCandidateDiagnostics> CandidateScores { get; init; }

    public double RegularityWeight { get; init; }
}

public sealed record SelfCalibratingAxisymmetricCandidateDiagnostics(
    double Lambda,
    double MeanFitError,
    double RegularityError,
    double Score);

public sealed class LeastSquaresAxisymmetricAlignmentDiagnostics
{
    public double InitialLambda { get; init; }
    public double RefinedLambda { get; init; }
    public double InitialMeanAlignmentError { get; init; }
    public double FinalMeanAlignmentError { get; init; }
    public double FinalRmsAlignmentError { get; init; }
    public double FinalMeanAngularErrorDegrees { get; init; }
    public double FinalMaxAngularErrorDegrees { get; init; }
    public int? MaxAngularErrorHoleIndex { get; init; }
    public int Iterations { get; init; }
    public bool Converged { get; init; }
    public bool UsesRegularization { get; init; }
    public IReadOnlyList<LeastSquaresAxisymmetricAlignmentIterationDiagnostics> IterationHistory { get; init; } = Array.Empty<LeastSquaresAxisymmetricAlignmentIterationDiagnostics>();
}

public sealed record LeastSquaresAxisymmetricAlignmentIterationDiagnostics(
    int Iteration,
    double Lambda,
    double MeanAlignmentError,
    double MeanAngularErrorDegrees);
