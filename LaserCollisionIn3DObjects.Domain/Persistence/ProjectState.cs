using System.Text.Json.Serialization;
using LaserCollisionIn3DObjects.Domain.Geometry;

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
    public List<PrismState> Prisms { get; set; } = new();
    public List<RayState> ManualRays { get; set; } = new();
    public List<CylindricalLightSourceState> CylindricalLightSources { get; set; } = new();
    public List<Point3> HolePoints { get; set; } = new();
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
    public PointSourceFrameStateDto SourceFrame { get; set; } = new();
    public List<ProjectionRayStateDto> Rays { get; set; } = new();
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
    public string SelectedMethodId { get; set; } = string.Empty;
}

public sealed class AnnotationWorkspaceState
{
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
