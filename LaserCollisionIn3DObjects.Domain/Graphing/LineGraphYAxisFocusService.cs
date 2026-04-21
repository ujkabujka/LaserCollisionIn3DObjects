namespace LaserCollisionIn3DObjects.Domain.Graphing;

public static class LineGraphYAxisFocusService
{
    public static (double Min, double Max)? TryComputeBounds(IReadOnlyList<GraphSeriesData> series, double paddingRatio = 0.05)
    {
        ArgumentNullException.ThrowIfNull(series);

        var yValues = series.SelectMany(item => item.Points.Select(point => point.Y)).ToList();
        if (yValues.Count == 0)
        {
            return null;
        }

        var min = yValues.Min();
        var max = yValues.Max();
        var span = max - min;
        var padding = span <= 0 ? Math.Max(1d, Math.Abs(max) * 0.05) : span * paddingRatio;
        return (min - padding, max + padding);
    }
}
