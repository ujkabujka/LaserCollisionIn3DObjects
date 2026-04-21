using LaserCollisionIn3DObjects.Domain.Geometry;
using LaserCollisionIn3DObjects.Domain.Projection;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public enum GraphableSourceKind
{
    CylindricalLightSource,
    ProjectionResult,
}

public sealed class GraphSceneData
{
    public required string SceneName { get; init; }
    public required IReadOnlyList<CylindricalLightSource> CylindricalSources { get; init; }
    public required IReadOnlyList<NamedProjectionResultState> ProjectionResults { get; init; }
}

public sealed class GraphableSourceData
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required GraphableSourceKind Kind { get; init; }
    public required Vector3 AxisX { get; init; }
    public required IReadOnlyList<Ray3D> Rays { get; init; }
    public Vector3? FrameOrigin { get; init; }
    public double? SourceLength { get; init; }
}

public sealed record AngleBinCount(double BinStartInclusiveDeg, double BinEndDeg, double BinCenterDeg, int Count);

public sealed record ScatterPointData(double X, double Y);

public sealed class GraphSeriesData
{
    public required string Name { get; init; }
    public IReadOnlyList<AngleBinCount> Bins { get; init; } = [];
    public IReadOnlyList<ScatterPointData> Points { get; init; } = [];
}

public enum GraphVisualizationKind
{
    GroupedBar,
    AngleBinXyLine,
    NormalizedAxialAngleXyLine,
}

public sealed class GraphResult
{
    public required GraphVisualizationKind VisualizationKind { get; init; }
    public required IReadOnlyList<GraphSeriesData> Series { get; init; }
}
