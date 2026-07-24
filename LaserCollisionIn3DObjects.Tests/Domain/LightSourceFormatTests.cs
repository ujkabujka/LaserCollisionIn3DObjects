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
        var formats = new LightSourceFormatRegistry().Formats;
        Assert.Contains(formats, format => format.Id == "canonical-text-v1");
        Assert.Contains(formats, format => format.Id == "spherical-direction-text-v1");
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
    [Theory]
    [InlineData(1f, 0f, 0f, 0f, 0f)]
    [InlineData(0f, 1f, 0f, 1.5707964f, 0f)]
    [InlineData(0f, 0f, 1f, 0f, 1.5707964f)]
    [InlineData(0f, 0f, -1f, 0f, -1.5707964f)]
    public void Src2_writes_expected_spherical_direction(float x, float y, float z, float azimuth, float elevation)
    {
        var source = Sample() with { Rays = new[] { new LightSourceTransferRay(Vector3.Zero, new Vector3(x, y, z)) } };
        var format = new SphericalDirectionLightSourceFormat(); using var stream = new MemoryStream(); format.Write(source, stream);
        var text = System.Text.Encoding.UTF8.GetString(stream.ToArray()); var line = Assert.Single(text.Split('\n').Where(value => value.StartsWith("Direction_Spherical 1:")));
        var values = line.Split(':')[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); Assert.Equal(azimuth, float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture), 5); Assert.Equal(elevation, float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture), 5);
        stream.Position = 0; Assert.Single(format.Read(stream).Rays);
    }

    [Fact]
    public void Src2_rejects_mismatched_spherical_direction()
    {
        const string content = "LASER_SOURCE_FORMAT: SRC2\nLASER_SOURCE_FORMAT_VERSION: 1\nNAME: Test\nSOURCE_KIND: Cylinder\nRAY_COUNT: 1\nFRAME_ORIGIN_WORLD: 0 0 0\nFRAME_AXIS_X_WORLD: 1 0 0\nFRAME_AXIS_Y_WORLD: 0 1 0\nFRAME_AXIS_Z_WORLD: 0 0 1\nGEOMETRY_BEGIN\nRADIUS: 1\nLENGTH: 1\nGEOMETRY_END\nRAYS_BEGIN\nPosition 1: 0 0 0\nDirection 1: 1 0 0\nDirection_Spherical 1: 1.57079632679 0\nRAYS_END";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)); var error = Assert.Throws<FormatException>(() => new SphericalDirectionLightSourceFormat().Read(stream)); Assert.Contains("does not match", error.Message);
    }

}
