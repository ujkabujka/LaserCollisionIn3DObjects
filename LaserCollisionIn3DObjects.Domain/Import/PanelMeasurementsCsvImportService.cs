using System.Globalization;
using System.Text;

namespace LaserCollisionIn3DObjects.Domain.Import;

public sealed record PanelCornerMeasurement(double DistanceMeters, double AzimuthDeg, double ElevationDeg);

public sealed record PanelMeasurementRecord(
    double WidthMm,
    double HeightMm,
    double ThicknessMm,
    PanelCornerMeasurement LeftTop,
    PanelCornerMeasurement RightTop,
    PanelCornerMeasurement RightBottom,
    PanelCornerMeasurement LeftBottom);

public sealed class PanelMeasurementsCsvImportService
{
    public const int ColumnCount = 15;

    public IReadOnlyList<PanelMeasurementRecord> Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var rows = ReadRows(reader).ToList();
        if (rows.Count == 0)
        {
            throw new FormatException("The CSV file contains no data rows.");
        }

        var startIndex = IsHeader(rows[0].Fields) ? 1 : 0;
        var results = new List<PanelMeasurementRecord>(rows.Count - startIndex);
        for (var i = startIndex; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Fields.Count != ColumnCount)
            {
                throw new FormatException($"CSV row {row.LineNumber} has {row.Fields.Count} columns; exactly {ColumnCount} are required.");
            }

            var fields = row.Fields;
            var values = fields.Select((value, index) => ReadNumber(value, row.LineNumber, ColumnNames[index])).ToArray();
            ValidatePositive(values[0], row.LineNumber, ColumnNames[0]);
            ValidatePositive(values[1], row.LineNumber, ColumnNames[1]);
            ValidatePositive(values[2], row.LineNumber, ColumnNames[2]);
            ValidatePositive(values[3], row.LineNumber, ColumnNames[3]);
            ValidatePositive(values[6], row.LineNumber, ColumnNames[6]);
            ValidatePositive(values[9], row.LineNumber, ColumnNames[9]);
            ValidatePositive(values[12], row.LineNumber, ColumnNames[12]);

            results.Add(new PanelMeasurementRecord(values[0], values[1], values[2],
                new PanelCornerMeasurement(values[3], values[4], values[5]),
                new PanelCornerMeasurement(values[6], values[7], values[8]),
                new PanelCornerMeasurement(values[9], values[10], values[11]),
                new PanelCornerMeasurement(values[12], values[13], values[14])));
        }

        return results;
    }

    private static readonly string[] ColumnNames = ["Width", "Height", "Thickness", "LT_R", "LT_Azimuth", "LT_Elevation", "RT_R", "RT_Azimuth", "RT_Elevation", "RB_R", "RB_Azimuth", "RB_Elevation", "LB_R", "LB_Azimuth", "LB_Elevation"];

    private static bool IsHeader(IReadOnlyList<string> fields)
    {
        if (fields.Count != ColumnCount) return false;
        return fields.Select(static field => field.Trim().TrimStart('\uFEFF'))
            .SequenceEqual(ColumnNames, StringComparer.OrdinalIgnoreCase);
    }

    private static double ReadNumber(string text, int lineNumber, string fieldName)
    {
        var normalized = text.Trim().TrimStart('\uFEFF');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        {
            throw new FormatException($"CSV row {lineNumber}, column '{fieldName}' must be a finite invariant-culture number.");
        }

        return value;
    }

    private static void ValidatePositive(double value, int lineNumber, string fieldName)
    {
        if (value <= 0)
        {
            throw new FormatException($"CSV row {lineNumber}, column '{fieldName}' must be greater than zero.");
        }
    }

    private static IEnumerable<CsvRow> ReadRows(TextReader reader)
    {
        var lineNumber = 1;
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var hasContent = false;
        while (true)
        {
            var next = reader.Read();
            if (next == -1)
            {
                if (quoted) throw new FormatException($"CSV row {lineNumber} has an unterminated quoted field.");
                if (hasContent || field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); yield return new CsvRow(lineNumber, fields); }
                yield break;
            }
            var c = (char)next;
            if (c == '"')
            {
                if (quoted && reader.Peek() == '"') { field.Append('"'); reader.Read(); }
                else quoted = !quoted;
                hasContent = true;
            }
            else if (c == ',' && !quoted) { fields.Add(field.ToString()); field.Clear(); hasContent = true; }
            else if ((c == '\r' || c == '\n') && !quoted)
            {
                if (c == '\r' && reader.Peek() == '\n') reader.Read();
                if (hasContent || field.Length > 0 || fields.Count > 0) { fields.Add(field.ToString()); yield return new CsvRow(lineNumber, fields); }
                fields = new List<string>(); field.Clear(); hasContent = false; lineNumber++;
            }
            else { field.Append(c); if (!char.IsWhiteSpace(c)) hasContent = true; }
        }
    }

    private sealed record CsvRow(int LineNumber, List<string> Fields);
}

public sealed class NaturalFileNameComparer : IComparer<string>
{
    public static NaturalFileNameComparer Instance { get; } = new();
    public int Compare(string? x, string? y)
    {
        x ??= string.Empty; y ??= string.Empty;
        var ix = 0; var iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                var sx = ix; while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                var sy = iy; while (iy < y.Length && char.IsDigit(y[iy])) iy++;
                var nx = x[sx..ix].TrimStart('0'); var ny = y[sy..iy].TrimStart('0');
                var result = nx.Length.CompareTo(ny.Length);
                if (result != 0) return result;
                result = string.Compare(nx, ny, StringComparison.Ordinal);
                if (result != 0) return result;
            }
            else
            {
                var result = char.ToUpperInvariant(x[ix]).CompareTo(char.ToUpperInvariant(y[iy]));
                if (result != 0) return result;
                ix++; iy++;
            }
        }
        return x.Length.CompareTo(y.Length);
    }
}
