using System.Globalization;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Domain.Export;

public sealed record ProjectionHitPointCsvImportResult(
    string SceneName,
    IReadOnlyList<Point3> HolePoints,
    int SkippedRowCount);

public sealed class ProjectionHitPointCsvImportService
{
    public ProjectionHitPointCsvImportResult Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
        {
            return new ProjectionHitPointCsvImportResult(string.Empty, Array.Empty<Point3>(), 0);
        }

        var header = ParseCsvLine(lines[0]);
        if (header.Count < 4 ||
            !string.Equals(header[0], "SceneName", StringComparison.Ordinal) ||
            !string.Equals(header[1], "HitX", StringComparison.Ordinal) ||
            !string.Equals(header[2], "HitY", StringComparison.Ordinal) ||
            !string.Equals(header[3], "HitZ", StringComparison.Ordinal))
        {
            throw new ArgumentException("CSV header must be exactly: SceneName,HitX,HitY,HitZ");
        }

        var points = new List<Point3>();
        var skipped = 0;
        string? sceneName = null;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                skipped++;
                continue;
            }

            var parts = ParseCsvLine(line);
            if (parts.Count < 4)
            {
                skipped++;
                continue;
            }

            var rowSceneName = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(rowSceneName))
            {
                skipped++;
                continue;
            }

            if (!double.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(parts[2], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var y) ||
                !double.TryParse(parts[3], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var z))
            {
                skipped++;
                continue;
            }

            if (double.IsNaN(x) || double.IsInfinity(x) ||
                double.IsNaN(y) || double.IsInfinity(y) ||
                double.IsNaN(z) || double.IsInfinity(z))
            {
                skipped++;
                continue;
            }

            sceneName ??= rowSceneName;
            points.Add(new Point3(x, y, z));
        }

        return new ProjectionHitPointCsvImportResult(sceneName ?? string.Empty, points, skipped);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && (i + 1) < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString());
        return result;
    }
}
