using System.Text;

namespace LaserCollisionIn3DObjects.Domain.Export;

/// <summary>Version 1 UTF-8 text format. Streams remain owned by the caller.</summary>
public sealed class CanonicalTextLightSourceFormat : ILightSourceFormat
{
    public string Id => "canonical-text-v1";
    public string DisplayName => "Laser Source Text";
    public IReadOnlyList<string> FileExtensions => new[] { ".txt" };
    public bool CanExport(LightSourceTransferData source, out string? reason) { reason = null; return source is not null; }
    public bool CanRead(string filePath, ReadOnlySpan<byte> header) => Encoding.UTF8.GetString(header).TrimStart('\uFEFF', ' ', '\t', '\r', '\n').StartsWith("LASER_SOURCE_FILE_VERSION:", StringComparison.Ordinal);
    public void Write(LightSourceTransferData source, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 1024, leaveOpen: true);
        writer.Write(CanonicalTextV1Serializer.Serialize(source));
    }
    public LightSourceTransferData Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
        return CanonicalTextV1Serializer.Parse(reader.ReadToEnd());
    }
}
