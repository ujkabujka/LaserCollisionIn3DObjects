using System.Text.Json.Serialization;
using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;

namespace LaserCollisionIn3DObjects.Domain.Persistence;

public static class PersistenceKeys
{
    public const string SchemaVersion = "schemaVersion";
    public const string Scenes = "scenes";
    public const string Collision = "collisionWorkspace";
    public const string Projection = "projectionWorkspace";
    public const string Annotation = "annotationWorkspace";
}

public sealed class ProjectState
{
    [JsonPropertyName(PersistenceKeys.SchemaVersion)]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName(PersistenceKeys.Scenes)]
    public List<SceneState> Scenes { get; set; } = new();

    [JsonPropertyName(PersistenceKeys.Collision)]
    public CollisionWorkspaceState CollisionWorkspace { get; set; } = new();

    [JsonPropertyName(PersistenceKeys.Projection)]
    public ProjectionWorkspaceStateDto ProjectionWorkspace { get; set; } = new();

    [JsonPropertyName(PersistenceKeys.Annotation)]
    public AnnotationWorkspaceState AnnotationWorkspace { get; set; } = new();
}

public sealed class SceneState
{
    public string Name { get; set; } = string.Empty;
    public bool IsProjectionOnly { get; set; }
    public List<PrismState> Prisms { get; set; } = new();
    public List<RayState> ManualRays { get; set; } = new();
    public List<CylindricalLightSourceState> CylindricalLightSources { get; set; } = new();
    public List<AxisymmetricLightSourceState> LightSources { get; set; } = new();
    public List<ProjectedLightSourceState> ProjectedLightSources { get; set; } = new();
    public List<Point3> HolePoints { get; set; } = new();
    public List<Point3> NaturalPoints { get; set; } = new();
    public List<Point3> MeasuredCornerPoints { get; set; } = new();
    public SceneProjectionStateDto Projection { get; set; } = new();
}

public sealed class PrismState
{
    public string Name { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float SizeX { get; set; }
    public float SizeY { get; set; }
    public float SizeZ { get; set; }
    public float? BaseOrientationX { get; set; }
    public float? BaseOrientationY { get; set; }
    public float? BaseOrientationZ { get; set; }
    public float? BaseOrientationW { get; set; }
    public List<AxisymmetricSourceSegmentStateDto> Segments { get; set; } = new();
}

public sealed class RayState
{
    public float OriginX { get; set; }
    public float OriginY { get; set; }
    public float OriginZ { get; set; }
    public float DirectionX { get; set; }
    public float DirectionY { get; set; }
    public float DirectionZ { get; set; }
}


public sealed class AxisymmetricLightSourceState
{
    public string Name { get; set; } = string.Empty;
    public AxisymmetricSourceKind SourceKind { get; set; } = AxisymmetricSourceKind.Cylinder;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float Radius { get; set; }
    public float Height { get; set; }
    public float RadiusStart { get; set; }
    public float RadiusEnd { get; set; }
    public float Length { get; set; }
    public float ArcRadius { get; set; }
    public OgiveCurvatureDirection OgiveCurvatureDirection { get; set; } = OgiveCurvatureDirection.Outward;
    public int RayCount { get; set; }
    public float TiltWeight { get; set; } = 0.1f;
    public float TiltPointX { get; set; }
    public float TiltPointY { get; set; }
    public float TiltPointZ { get; set; }
    public float? BaseOrientationX { get; set; }
    public float? BaseOrientationY { get; set; }
    public float? BaseOrientationZ { get; set; }
    public float? BaseOrientationW { get; set; }
    public List<AxisymmetricSourceSegmentStateDto> Segments { get; set; } = new();
}

public sealed class AxisymmetricSourceSegmentStateDto
{
    public HybridAxisymmetricSourceSegmentKind SegmentKind { get; set; } = HybridAxisymmetricSourceSegmentKind.Cylinder;
    public float Length { get; set; }
    public float RadiusStart { get; set; }
    public float RadiusEnd { get; set; }
    public float? ArcRadius { get; set; }
    public OgiveCurvatureDirection OgiveCurvatureDirection { get; set; } = OgiveCurvatureDirection.Outward;
}


public sealed class ProjectedLightSourceState
{
    public string Name { get; set; } = string.Empty;
    public PointSourceFrameStateDto SourceFrame { get; set; } = new();
    public AxisymmetricSourceProfileDefinition ProfileDefinition { get; set; } = new();
    public List<ProjectionRayStateDto> Rays { get; set; } = new();
    public List<RayState> ExactRays { get; set; } = new();
    public float? BaseOrientationX { get; set; }
    public float? BaseOrientationY { get; set; }
    public float? BaseOrientationZ { get; set; }
    public float? BaseOrientationW { get; set; }
    public string OriginKind { get; set; } = string.Empty;
}

public sealed class CylindricalLightSourceState
{
    public string Name { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float Radius { get; set; }
    public float Height { get; set; }
    public int RayCount { get; set; }
    public float TiltWeight { get; set; } = 0.1f;
    public float TiltPointX { get; set; }
    public float TiltPointY { get; set; }
    public float TiltPointZ { get; set; }
    public float? BaseOrientationX { get; set; }
    public float? BaseOrientationY { get; set; }
    public float? BaseOrientationZ { get; set; }
    public float? BaseOrientationW { get; set; }
    public List<AxisymmetricSourceSegmentStateDto> Segments { get; set; } = new();
}

public sealed class SceneProjectionStateDto
{
    public string SelectedMethodId { get; set; } = string.Empty;
    public string? SelectedResultKey { get; set; }
    public List<ProjectionResultStateDto> Results { get; set; } = new();
}

public sealed class ProjectionResultStateDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MethodId { get; set; } = string.Empty;
    public Point3? PointSourceOrigin { get; set; }
    public PointSourceFrameStateDto SourceFrame { get; set; } = new();
    public List<ProjectionRayStateDto> Rays { get; set; } = new();
    public AxisymmetricProjectionStateDto? AxisymmetricSource { get; set; }
}

public sealed class AxisymmetricProjectionStateDto
{
    public PointSourceFrameStateDto SourceFrame { get; set; } = new();
    public AxisymmetricSourceProfileDefinition ProfileDefinition { get; set; } = new();
    public Point3? LocalTiltPoint { get; set; }
    public double? EstimatedTiltWeight { get; set; }
    public SelfCalibratingAxisymmetricProjectionDiagnosticsDto? Diagnostics { get; set; }
    public LeastSquaresAxisymmetricAlignmentDiagnosticsDto? LeastSquaresDiagnostics { get; set; }
    public List<AxisymmetricProjectionPointStateDto> Points { get; set; } = new();
}

public sealed class AxisymmetricProjectionPointStateDto
{
    public Point3 HolePoint { get; set; }
    public Point3 SourceSurfacePoint { get; set; }
    public Point3 RayOrigin { get; set; }
    public Vector3D RayDirection { get; set; }
    public Vector3D? ModeledRayDirection { get; set; }
    public double? LocalU { get; set; }
    public double? LocalTheta { get; set; }
    public double? UnwrappedU { get; set; }
    public double? UnwrappedV { get; set; }
    public double? FitError { get; set; }
    public double? AlignmentError { get; set; }
    public double? AngularErrorDegrees { get; set; }
}

public sealed class SelfCalibratingAxisymmetricProjectionDiagnosticsDto
{
    public double RegularityWeight { get; set; }
    public List<SelfCalibratingAxisymmetricCandidateDiagnosticsDto> CandidateScores { get; set; } = new();
}

public sealed class SelfCalibratingAxisymmetricCandidateDiagnosticsDto
{
    public double Lambda { get; set; }
    public double MeanFitError { get; set; }
    public double RegularityError { get; set; }
    public double Score { get; set; }
}

public sealed class LeastSquaresAxisymmetricAlignmentDiagnosticsDto
{
    public double InitialLambda { get; set; }
    public double RefinedLambda { get; set; }
    public double InitialMeanAlignmentError { get; set; }
    public double FinalMeanAlignmentError { get; set; }
    public double FinalRmsAlignmentError { get; set; }
    public double FinalMeanAngularErrorDegrees { get; set; }
    public double FinalMaxAngularErrorDegrees { get; set; }
    public int? MaxAngularErrorHoleIndex { get; set; }
    public int Iterations { get; set; }
    public bool Converged { get; set; }
    public bool UsesRegularization { get; set; }
    public List<AxisymmetricLeastSquaresIterationDiagnosticsDto> IterationHistory { get; set; } = new();
}

public sealed class AxisymmetricLeastSquaresIterationDiagnosticsDto
{
    public int Iteration { get; set; }
    public double ObjectiveError { get; set; }
    public double Lambda { get; set; }
    public double MeanAlignmentError { get; set; }
    public double RmsAlignmentError { get; set; }
    public double MeanAngularErrorDegrees { get; set; }
    public double MaxAngularErrorDegrees { get; set; }
    public bool Improved { get; set; }
}

public sealed class PointSourceFrameStateDto
{
    public Point3 Origin { get; set; }
    public Vector3D AxisX { get; set; }
    public Vector3D AxisY { get; set; }
    public Vector3D AxisZ { get; set; }
}

public sealed class ProjectionRayStateDto
{
    public RayState Ray { get; set; } = new();
    public Point3 TargetHolePoint { get; set; }
}

public sealed class CollisionWorkspaceState
{
    public string? SelectedSceneName { get; set; }
}

public sealed class ProjectionWorkspaceStateDto
{
    public string? SelectedSceneName { get; set; }
    public bool? ShowPanels { get; set; }
    public bool? ShowMeasuredCorners { get; set; }
    public bool? IncludeNaturalPoints { get; set; }
    public string SelectedMethodId { get; set; } = string.Empty;
    public AxisymmetricSourceKind ProjectionGeometryKind { get; set; } = AxisymmetricSourceKind.Cylinder;
    public double GeometryRadiusStart { get; set; } = 1d;
    public double GeometryRadiusEnd { get; set; } = 1d;
    public double GeometryLength { get; set; } = 10d;
    public double GeometryArcRadius { get; set; } = 20d;
    public OgiveCurvatureDirection GeometryOgiveCurvatureDirection { get; set; } = OgiveCurvatureDirection.Outward;
    public int HybridSegmentCount { get; set; } = 1;
    public List<AxisymmetricSourceSegmentStateDto> HybridSegments { get; set; } = new();
    public int HybridRayCount { get; set; } = 200;
    public float HybridTiltWeight { get; set; } = 0.1f;
    public double TiltPointX { get; set; }
    public double TiltPointY { get; set; }
    public double TiltPointZ { get; set; }
    public int? LeastSquaresMaxIterations { get; set; }
    public double? LeastSquaresConvergenceTolerance { get; set; }
    public double? LeastSquaresPointStepScale { get; set; }
    public double? LeastSquaresThetaStepScale { get; set; }
    public double? LeastSquaresLambdaStepScale { get; set; }
    public float HybridTiltPointX { get; set; }
    public float HybridTiltPointY { get; set; }
    public float HybridTiltPointZ { get; set; }
}

public sealed class AnnotationWorkspaceState
{
    public string? PrismGenerationMethodology { get; set; }
    public string? FolderPath { get; set; }
    public bool IsFolderResolved { get; set; }
    public double GlobalPanelWidthMm { get; set; }
    public double GlobalPanelHeightMm { get; set; }
    public double GlobalPanelThicknessMm { get; set; }
    public List<AnnotationImageState> Images { get; set; } = new();
}

public sealed class AnnotationImageState
{
    public string FileName { get; set; } = string.Empty;
    public double? PanelWidthMm { get; set; }
    public double? PanelHeightMm { get; set; }
    public double? PanelThicknessMm { get; set; }
    public List<AnnotationCornerState> Corners { get; set; } = new();
}

public sealed class AnnotationCornerState
{
    public string CornerType { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public double? ManualAzimuthDeg { get; set; }
    public double? ManualElevationDeg { get; set; }
    public double? ManualDistanceMeters { get; set; }
    public double? DirectX { get; set; }
    public double? DirectY { get; set; }
    public double? DirectZ { get; set; }
}
