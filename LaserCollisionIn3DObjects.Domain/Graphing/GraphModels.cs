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
}

public sealed record AngleBinCount(double BinStartInclusiveDeg, double BinEndDeg, double BinCenterDeg, int Count);

public sealed record GraphSeriesData(string Name, IReadOnlyList<AngleBinCount> Bins);

public enum GraphVisualizationKind
{
    GroupedBar,
    Xy,
}

public sealed class GraphResult
{
    public required GraphVisualizationKind VisualizationKind { get; init; }
    public required IReadOnlyList<GraphSeriesData> Series { get; init; }
}
