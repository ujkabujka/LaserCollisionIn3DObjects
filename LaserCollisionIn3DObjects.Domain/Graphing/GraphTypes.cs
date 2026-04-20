using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed class GraphBuildContext
{
    public required IReadOnlyList<GraphableSourceData> Sources { get; init; }
    public required double BinSizeDeg { get; init; }
}

public interface IGraphType
{
    string Id { get; }
    string DisplayName { get; }
    GraphResult Build(GraphBuildContext context);
}

public sealed class GraphTypeRegistry
{
    private readonly IReadOnlyList<IGraphType> _graphTypes;

    public GraphTypeRegistry(IEnumerable<IGraphType> graphTypes)
    {
        _graphTypes = graphTypes?.ToList() ?? throw new ArgumentNullException(nameof(graphTypes));
        if (_graphTypes.Count == 0)
        {
            throw new ArgumentException("At least one graph type is required.", nameof(graphTypes));
        }
    }

    public IReadOnlyList<IGraphType> GraphTypes => _graphTypes;

    public IGraphType Resolve(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _graphTypes.FirstOrDefault(type => string.Equals(type.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Graph type '{id}' is not registered.");
    }
}

public sealed class AngleBinBarChartGraphType : IGraphType
{
    private readonly AngleHistogramService _histogramService;

    public AngleBinBarChartGraphType(AngleHistogramService? histogramService = null)
    {
        _histogramService = histogramService ?? new AngleHistogramService();
    }

    public string Id => "graph.angle-bin-bar";
    public string DisplayName => "Angle-bin bar chart";

    public GraphResult Build(GraphBuildContext context)
    {
        var series = BuildSeries(context);
        return new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.GroupedBar,
            Series = series,
        };
    }

    private IReadOnlyList<GraphSeriesData> BuildSeries(GraphBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Sources
            .Select(source => new GraphSeriesData
            {
                Name = source.DisplayName,
                Bins = _histogramService.CreateHistogram(source.Rays, source.AxisX, context.BinSizeDeg),
            })
            .ToList();
    }
}

public sealed class AngleBinXyChartGraphType : IGraphType
{
    private readonly AngleHistogramService _histogramService;

    public AngleBinXyChartGraphType(AngleHistogramService? histogramService = null)
    {
        _histogramService = histogramService ?? new AngleHistogramService();
    }

    public string Id => "graph.angle-bin-xy";
    public string DisplayName => "Angle-bin XY chart";

    public GraphResult Build(GraphBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var series = context.Sources
            .Select(source => new GraphSeriesData
            {
                Name = source.DisplayName,
                Bins = _histogramService.CreateHistogram(source.Rays, source.AxisX, context.BinSizeDeg),
            })
            .ToList();

        return new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.Xy,
            Series = series,
        };
    }
}

public sealed class CylindricalNormalizedAxialAngleXyGraphType : IGraphType
{
    public string Id => "graph.cylindrical-normalized-axial-angle-xy";
    public string DisplayName => "Cylindrical normalized axial-angle XY";

    public GraphResult Build(GraphBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var compatibleSources = context.Sources
            .Where(source => source.Kind == GraphableSourceKind.CylindricalLightSource
                             && source.SourceLength is > 0d
                             && source.FrameOrigin is not null)
            .ToList();

        var series = compatibleSources
            .Select(source => new GraphSeriesData
            {
                Name = source.DisplayName,
                Points = source.Rays
                    .Select(ray => BuildPoint(source, ray))
                    .ToList(),
            })
            .ToList();

        return new GraphResult
        {
            VisualizationKind = GraphVisualizationKind.Xy,
            Series = series,
        };
    }

    private static ScatterPointData BuildPoint(GraphableSourceData source, Geometry.Ray3D ray)
    {
        var axis = NormalizeOrThrow(source.AxisX, nameof(source.AxisX));
        var length = source.SourceLength ?? throw new InvalidOperationException("Source length is required.");
        var frameOrigin = source.FrameOrigin ?? throw new InvalidOperationException("Source frame origin is required.");

        var localX = Vector3.Dot(ray.Origin - frameOrigin, axis);
        var normalizedX = localX / length;
        var angle = AngleHistogramService.CalculateAngleDegrees(axis, ray.Direction);
        return new ScatterPointData(normalizedX, angle);
    }

    private static Vector3 NormalizeOrThrow(Vector3 value, string parameterName)
    {
        if (value.LengthSquared() <= 0f)
        {
            throw new ArgumentException("Vector must be non-zero.", parameterName);
        }

        return Vector3.Normalize(value);
    }
}
