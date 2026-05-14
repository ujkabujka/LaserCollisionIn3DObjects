using LaserCollisionIn3DObjects.Domain.Geometry;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed class AzimuthPolarHeatmapService
{
    public HeatmapGridData Create(
        string name,
        IReadOnlyList<Ray3D> rays,
        Vector3 localAxisX,
        Vector3 localAxisY,
        Vector3 localAxisZ,
        double azimuthBinSizeDeg,
        double polarBinSizeDeg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rays);

        if (azimuthBinSizeDeg <= 0 || azimuthBinSizeDeg > 360)
        {
            throw new ArgumentOutOfRangeException(nameof(azimuthBinSizeDeg), "Azimuth bin size must be in the range (0, 360].");
        }

        if (polarBinSizeDeg <= 0 || polarBinSizeDeg > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(polarBinSizeDeg), "Polar bin size must be in the range (0, 180].");
        }

        var azimuthBinCount = Math.Max(1, (int)Math.Ceiling(360d / azimuthBinSizeDeg));
        var polarBinCount = Math.Max(1, (int)Math.Ceiling(180d / polarBinSizeDeg));
        var values = new double[azimuthBinCount, polarBinCount];

        foreach (var ray in rays)
        {
            var azimuth = RayAngleMath.CalculateAzimuthDegrees(localAxisX, localAxisY, localAxisZ, ray.Direction);
            var polar = RayAngleMath.CalculatePolarDegrees(localAxisX, ray.Direction);
            var azimuthIndex = Math.Min(azimuthBinCount - 1, (int)Math.Floor(azimuth / azimuthBinSizeDeg));
            var polarIndex = Math.Min(polarBinCount - 1, (int)Math.Floor(polar / polarBinSizeDeg));
            values[azimuthIndex, polarIndex] += 1d;
        }

        return new HeatmapGridData
        {
            Name = name,
            XMin = 0,
            XMax = 360,
            YMin = 0,
            YMax = 180,
            Values = values,
        };
    }
}
