using System.IO;
using LaserCollisionIn3DObjects.Domain.Export;

namespace LaserCollisionIn3DObjects.Wpf.Services;

/// <summary>Coordinates discovered file formats, dialog metadata, and stream-based import/export.</summary>
public sealed class LightSourceFileService
{
    private const int ProbeLength = 4096;
    private readonly LightSourceFormatRegistry _registry;
    public LightSourceFileService(LightSourceFormatRegistry? registry = null) => _registry = registry ?? new LightSourceFormatRegistry();
    public IReadOnlyList<ILightSourceFormat> Formats => _registry.Formats;
    public IReadOnlyList<ILightSourceFormat> GetExportFormats(LightSourceTransferData source) => Formats.Where(format => format.CanExport(source, out _)).ToArray();
    public string BuildExportFilter(IReadOnlyList<ILightSourceFormat> formats) => string.Join("|", formats.Select(FilterPart));
    public string BuildImportFilter()
    {
        var patterns = Formats.SelectMany(format => Patterns(format)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var items = new List<string> { $"All Supported Light Source Files ({string.Join(";", patterns)})|{string.Join(";", patterns)}" };
        items.AddRange(Formats.Select(FilterPart)); items.Add("All Files (*.*)|*.*"); return string.Join("|", items);
    }
    public string DefaultExtension(ILightSourceFormat format) => NormalizeExtension(format.FileExtensions[0]);
    public ILightSourceFormat GetExportFormat(IReadOnlyList<ILightSourceFormat> formats, int filterIndex) => formats.ElementAtOrDefault(filterIndex - 1) ?? throw new InvalidOperationException("The selected light-source format is unavailable.");
    public void Write(ILightSourceFormat format, LightSourceTransferData source, string path) { using var stream = File.Create(path); format.Write(source, stream); }
    public (LightSourceTransferData Data, ILightSourceFormat Format) Read(string path)
    {
        var extension = Path.GetExtension(path); var candidates = Formats.Where(format => format.FileExtensions.Any(value => string.Equals(NormalizeExtension(value), extension, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (candidates.Length == 0) candidates = Formats.ToArray();
        var header = ReadHeader(path); var matches = candidates.Where(format => format.CanRead(path, header)).ToArray();
        if (matches.Length == 0) throw new FormatException($"Unsupported light source file format: '{Path.GetFileName(path)}'.");
        if (matches.Length > 1) throw new FormatException($"Ambiguous light source file format: '{Path.GetFileName(path)}'.");
        using var stream = File.OpenRead(path); return (matches[0].Read(stream), matches[0]);
    }
    private static byte[] ReadHeader(string path) { using var stream = File.OpenRead(path); var bytes = new byte[Math.Min(ProbeLength, (int)Math.Min(stream.Length, ProbeLength))]; _ = stream.Read(bytes); return bytes; }
    private static string FilterPart(ILightSourceFormat format) { var patterns = Patterns(format).ToArray(); return $"{format.DisplayName} ({string.Join(";", patterns)})|{string.Join(";", patterns)}"; }
    private static IEnumerable<string> Patterns(ILightSourceFormat format) => format.FileExtensions.Select(extension => "*" + NormalizeExtension(extension));
    private static string NormalizeExtension(string extension) => "." + extension.Trim().TrimStart('.');
}
