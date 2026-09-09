using System.Globalization;
using System.Text;
using LaserCollisionIn3DObjects.Domain.Collision;

namespace LaserCollisionIn3DObjects.Domain.Export;

public sealed class PanelCollisionCsvExportService
{
    public void Export(string filePath, PanelCollisionAnalysis analysis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(analysis);
        File.WriteAllText(filePath, CreateCsv(analysis), new UTF8Encoding(true));
    }

    public string CreateCsv(PanelCollisionAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var lines = new List<string> { "SceneName,PanelName,PanelHitIndex,RayIndex,SourceType,SourceName,LocalY,LocalZ,WorldX,WorldY,WorldZ" };
        foreach (var hit in analysis.Panels.SelectMany(panel => panel.Hits))
        {
            lines.Add(string.Join(',', Escape(analysis.SceneName), Escape(hit.PanelName), hit.PanelHitIndex, hit.RayIndex,
                Escape(hit.SourceType.ToString()), Escape(hit.SourceName), Format(hit.LocalY), Format(hit.LocalZ),
                Format(hit.WorldHitPoint.X), Format(hit.WorldHitPoint.Y), Format(hit.WorldHitPoint.Z)));
        }
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) < 0
        ? value
        : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
