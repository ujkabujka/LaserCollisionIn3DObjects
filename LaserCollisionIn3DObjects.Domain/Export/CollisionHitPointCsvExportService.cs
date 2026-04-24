using System.Globalization;
using System.Text;

namespace LaserCollisionIn3DObjects.Domain.Export;

public sealed class CollisionHitPointCsvExportService
{
    public void Export(string filePath, IEnumerable<CollisionHitPointRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(records);

        var lines = new List<string> { "SceneName,HitX,HitY,HitZ" };
        lines.AddRange(records.Select(record =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{EscapeCsv(record.SceneName)},{record.HitPoint.X},{record.HitPoint.Y},{record.HitPoint.Z}")));

        File.WriteAllLines(filePath, lines, Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
