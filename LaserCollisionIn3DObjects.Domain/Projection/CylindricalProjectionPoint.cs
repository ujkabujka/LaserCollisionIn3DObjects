using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Projection;

public sealed record CylindricalProjectionPoint(
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
}

public sealed class CylindricalProjectionState
{
    public required PointSourceFrameState SourceFrame { get; init; }

    public required double Radius { get; init; }

    public required double Length { get; init; }

    public Point3? LocalTiltPoint { get; init; }

    public double? EstimatedTiltWeight { get; init; }

    public SelfCalibratingCylindricalProjectionDiagnostics? Diagnostics { get; init; }

    public required IReadOnlyList<CylindricalProjectionPoint> Points { get; init; }
}

public sealed class SelfCalibratingCylindricalProjectionDiagnostics
{
    public required IReadOnlyList<SelfCalibratingCylindricalCandidateDiagnostics> CandidateScores { get; init; }

    public double RegularityWeight { get; init; }
}

public sealed record SelfCalibratingCylindricalCandidateDiagnostics(
    double Lambda,
    double MeanFitError,
    double RegularityError,
    double Score);
