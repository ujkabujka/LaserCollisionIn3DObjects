namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed class StoredGraphChart
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string GraphTypeId { get; init; }
    public required double BinSizeDeg { get; init; }
    public required IReadOnlyList<string> SelectedSourceIds { get; init; }
}

public sealed class StoredGraphChartSession
{
    private readonly List<StoredGraphChart> _charts = [];

    public IReadOnlyList<StoredGraphChart> Charts => _charts;

    public StoredGraphChart? SelectedChart { get; private set; }

    public void AddAndSelect(StoredGraphChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        _charts.Add(chart);
        SelectedChart = chart;
    }

    public bool Select(string chartId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chartId);
        var next = _charts.FirstOrDefault(chart => chart.Id == chartId);
        if (next is null)
        {
            return false;
        }

        SelectedChart = next;
        return true;
    }

    public StoredGraphChart? DeleteSelected()
    {
        if (SelectedChart is null)
        {
            return null;
        }

        var current = SelectedChart;
        var index = _charts.IndexOf(current);
        _charts.RemoveAt(index);

        if (_charts.Count == 0)
        {
            SelectedChart = null;
            return current;
        }

        var nextIndex = Math.Clamp(index, 0, _charts.Count - 1);
        SelectedChart = _charts[nextIndex];
        return current;
    }
}
