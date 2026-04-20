using LaserCollisionIn3DObjects.Domain.Geometry;
using System.Numerics;

namespace LaserCollisionIn3DObjects.Domain.Graphing;

public sealed class AngleHistogramService
{
    public IReadOnlyList<AngleBinCount> CreateHistogram(IReadOnlyList<Ray3D> rays, Vector3 sourceAxisX, double binSizeDeg)
    {
        ArgumentNullException.ThrowIfNull(rays);

        if (binSizeDeg <= 0 || binSizeDeg > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(binSizeDeg), "Bin size must be in the range (0, 180].");
        }

        var axis = NormalizeOrThrow(sourceAxisX, nameof(sourceAxisX));
        var binCount = (int)Math.Ceiling(180d / binSizeDeg);
        var counts = new int[binCount];

        foreach (var ray in rays)
        {
            var angle = CalculateAngleDegrees(axis, ray.Direction);
            var index = ResolveBinIndex(angle, binSizeDeg, binCount);
            counts[index]++;
        }

        var bins = new List<AngleBinCount>(binCount);
        for (var i = 0; i < binCount; i++)
        {
            var start = i * binSizeDeg;
            var end = i == binCount - 1 ? 180d : Math.Min(180d, (i + 1) * binSizeDeg);
            bins.Add(new AngleBinCount(start, end, (start + end) / 2d, counts[i]));
        }

        return bins;
    }

    public static double CalculateAngleDegrees(Vector3 sourceAxisX, Vector3 rayDirection)
    {
        var axis = NormalizeOrThrow(sourceAxisX, nameof(sourceAxisX));
        var direction = NormalizeOrThrow(rayDirection, nameof(rayDirection));
        var dot = Math.Clamp(Vector3.Dot(axis, direction), -1f, 1f);
        return Math.Acos(dot) * (180d / Math.PI);
    }

    private static int ResolveBinIndex(double angleDeg, double binSizeDeg, int binCount)
    {
        if (angleDeg >= 180d)
        {
            return binCount - 1;
        }

        var index = (int)(angleDeg / binSizeDeg);
        return Math.Clamp(index, 0, binCount - 1);
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
