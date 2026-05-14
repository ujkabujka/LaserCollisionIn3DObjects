using LaserCollisionIn3DObjects.Domain.Geometry;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed class AzimuthHistogramService
{
    public IReadOnlyList<AngleBinCount> CreateHistogram(
        IReadOnlyList<Ray3D> rays,
        Vector3 localAxisX,
        Vector3 localAxisY,
        Vector3 localAxisZ,
        double binSizeDeg)
    {
        ArgumentNullException.ThrowIfNull(rays);
        if (binSizeDeg <= 0 || binSizeDeg > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(binSizeDeg), "Bin size must be in the range (0, 360].");
        }

        var binCount = Math.Max(1, (int)Math.Ceiling(360d / binSizeDeg));
        var counts = new int[binCount];

        foreach (var ray in rays)
        {
            var azimuth = RayAngleMath.CalculateAzimuthDegrees(localAxisX, localAxisY, localAxisZ, ray.Direction);
            var index = (int)Math.Floor(azimuth / binSizeDeg);
            if (index >= counts.Length)
            {
                index = counts.Length - 1;
            }

            counts[index]++;
        }

        var bins = new List<AngleBinCount>(binCount);
        for (var i = 0; i < binCount; i++)
        {
            var start = i * binSizeDeg;
            var end = Math.Min(360d, start + binSizeDeg);
            bins.Add(new AngleBinCount(start, end, (start + end) / 2d, counts[i]));
        }

        return bins;
    }
}
