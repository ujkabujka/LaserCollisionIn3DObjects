using System.Numerics;
using System.Reflection;
using LaserCollisionIn3DObjects.Domain.Export;
using LaserCollisionIn3DObjects.Domain.Geometry;

namespace LaserCollisionIn3DObjects.Tests.Domain;

public sealed class LightSourceFormatTests
{
    [Fact]
    public void Registry_discovers_canonical_text_format()
    {
        var format = Assert.Single(new LightSourceFormatRegistry().Formats);
        Assert.Equal("canonical-text-v1", format.Id);
        Assert.Equal("Laser Source Text", format.DisplayName);
        Assert.Contains(".txt", format.FileExtensions);
    }

    [Fact]
    public void Registry_accepts_additional_format_without_a_central_list()
    {
        var formats = new LightSourceFormatRegistry(new ILightSourceFormat[] { new TestFormat() }).Formats;
        Assert.Contains(formats, format => format is TestFormat);
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new LightSourceFormatRegistry(new ILightSourceFormat[] { new DuplicateFormatA(), new DuplicateFormatB() }));
        Assert.Contains("Duplicate light-source format ID", error.Message);
    }

    [Fact]
    public void Canonical_text_round_trips_and_leaves_streams_open()
    {
        var source = Sample(); var format = new CanonicalTextLightSourceFormat();
        using var stream = new MemoryStream();
        format.Write(source, stream);
        Assert.True(stream.CanWrite);
        stream.Position = 0;
        var restored = format.Read(stream);
        Assert.True(stream.CanRead);
        Assert.Equal(source.Name, restored.Name);
        Assert.Single(restored.Rays);
        Assert.Contains("LASER_SOURCE_FILE_VERSION: 1", System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static LightSourceTransferData Sample() => new("Example", new AxisymmetricSourceProfileDefinition { Kind = AxisymmetricSourceKind.Cylinder, Radius = 2, Length = 3 }, new Frame3D(), 0, Vector3.Zero, new[] { new LightSourceTransferRay(Vector3.One, Vector3.UnitX) });

    public sealed class TestFormat : StubFormat { public override string Id => "test-format"; }
    public sealed class DuplicateFormatA : StubFormat { public override string Id => "duplicate"; }
    public sealed class DuplicateFormatB : StubFormat { public override string Id => "duplicate"; }
    public abstract class StubFormat : ILightSourceFormat
    {
        public abstract string Id { get; } public string DisplayName => Id; public IReadOnlyList<string> FileExtensions => new[] { ".test" };
        public bool CanExport(LightSourceTransferData source, out string? reason) { reason = null; return true; }
        public bool CanRead(string filePath, ReadOnlySpan<byte> header) => false;
        public void Write(LightSourceTransferData source, Stream output) { }
        public LightSourceTransferData Read(Stream input) => Sample();
    }
}
