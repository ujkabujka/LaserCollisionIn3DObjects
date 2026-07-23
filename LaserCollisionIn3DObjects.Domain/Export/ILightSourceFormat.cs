namespace LaserCollisionIn3DObjects.Domain.Export;

/// <summary>
/// A built-in portable light-source file format. Implement this interface in a concrete class with a
/// public parameterless constructor and rebuild: <see cref="LightSourceFormatRegistry"/> discovers it
/// automatically. Format implementations operate only on <see cref="LightSourceTransferData"/>.
/// </summary>
public interface ILightSourceFormat
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyList<string> FileExtensions { get; }
    bool CanExport(LightSourceTransferData source, out string? reason);
    bool CanRead(string filePath, ReadOnlySpan<byte> header);
    void Write(LightSourceTransferData source, Stream output);
    LightSourceTransferData Read(Stream input);
}
