using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace LaserCollisionIn3DObjects.Domain.Export;

/// <summary>SRC2 text format: canonical source data plus validated spherical ray directions.</summary>
public sealed class SphericalDirectionLightSourceFormat : ILightSourceFormat
{
    private const float DirectionTolerance = 1e-5f;
    private const float PoleTolerance = 1e-6f;
    private static readonly Regex Direction = new("^Direction (\\d+): (.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex Spherical = new("^Direction_Spherical (\\d+): (.+)$", RegexOptions.CultureInvariant);
    public string Id => "spherical-direction-text-v1";
    public string DisplayName => "Laser Source Text with Spherical Directions";
    public IReadOnlyList<string> FileExtensions => new[] { ".src2" };
    public bool CanExport(LightSourceTransferData source, out string? reason)
    {
        if (source is null) { reason = "Source data is required."; return false; }
        try { _ = CanonicalTextV1Serializer.Serialize(source); reason = null; return true; }
        catch (Exception exception) { reason = exception.Message; return false; }
    }
    public bool CanRead(string filePath, ReadOnlySpan<byte> header)
    {
        try { return Encoding.UTF8.GetString(header).TrimStart('\uFEFF', ' ', '\t', '\r', '\n').StartsWith("LASER_SOURCE_FORMAT: SRC2", StringComparison.Ordinal); }
        catch { return false; }
    }
    public void Write(LightSourceTransferData source, Stream output)
    {
        if (!CanExport(source, out var reason)) throw new ArgumentException(reason, nameof(source));
        using var writer = new StreamWriter(output, new UTF8Encoding(false), 1024, leaveOpen: true);
        var canonical = CanonicalTextV1Serializer.Serialize(source);
        writer.Write(ToSrc2(canonical));
    }
    public LightSourceTransferData Read(Stream input)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, true, 1024, leaveOpen: true);
        var text = reader.ReadToEnd();
        return CanonicalTextV1Serializer.Parse(ToCanonicalAfterValidation(text));
    }
    private static string ToSrc2(string canonical)
    {
        var lines = canonical.Replace("\r\n", "\n").Split('\n'); var result = new StringBuilder();
        result.AppendLine("LASER_SOURCE_FORMAT: SRC2").AppendLine("LASER_SOURCE_FORMAT_VERSION: 1");
        foreach (var raw in lines.Skip(1))
        {
            result.AppendLine(raw);
            if (raw == "RAY_DIRECTION_COORDINATE_SYSTEM: SOURCE_LOCAL") result.AppendLine("SPHERICAL_ANGLE_UNIT: RADIANS").AppendLine("SPHERICAL_AZIMUTH_CONVENTION: ATAN2_Y_X").AppendLine("SPHERICAL_ELEVATION_CONVENTION: ANGLE_FROM_XY_PLANE");
            var match = Direction.Match(raw); if (match.Success)
            {
                var direction = Vector(match.Groups[2].Value, 0, "Direction"); var (azimuth, elevation) = ToSpherical(Vector3.Normalize(direction));
                result.Append("Direction_Spherical ").Append(match.Groups[1].Value).Append(": ").Append(azimuth.ToString("R", CultureInfo.InvariantCulture)).Append(' ').Append(elevation.ToString("R", CultureInfo.InvariantCulture)).AppendLine();
            }
        }
        return result.ToString();
    }
    private static string ToCanonicalAfterValidation(string src2)
    {
        var lines = src2.Replace("\r\n", "\n").Split('\n'); if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF', ' ', '\t').Equals("LASER_SOURCE_FORMAT: SRC2", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Line 1: expected LASER_SOURCE_FORMAT: SRC2.");
        var rays = new Dictionary<int, (Vector3? Cartesian, (float Azimuth, float Elevation)? Spherical, int SphericalLine)>(); var canonical = new StringBuilder("LASER_SOURCE_FILE_VERSION: 1\n");
        for (var i = 2; i < lines.Length; i++)
        {
            var line = lines[i]; var number = i + 1; var spherical = Spherical.Match(line.Trim());
            if (spherical.Success) { var index = int.Parse(spherical.Groups[1].Value, CultureInfo.InvariantCulture); var values = spherical.Groups[2].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); if (values.Length != 2) throw new FormatException($"Line {number}: Direction_Spherical {index} must contain azimuth and elevation."); var azimuth = Number(values[0], number, "azimuth"); var elevation = Number(values[1], number, "elevation"); if (azimuth < -MathF.PI - DirectionTolerance || azimuth > MathF.PI + DirectionTolerance) throw new FormatException($"Line {number}: azimuth must be between -π and π."); if (elevation < -MathF.PI / 2 - DirectionTolerance || elevation > MathF.PI / 2 + DirectionTolerance) throw new FormatException($"Line {number}: elevation must be between -π/2 and π/2."); rays.TryGetValue(index, out var ray); if (ray.Spherical is not null) throw new FormatException($"Line {number}: duplicate Direction_Spherical {index}."); ray.Spherical = (azimuth, elevation); ray.SphericalLine = number; rays[index] = ray; continue; }
            var direction = Direction.Match(line.Trim()); if (direction.Success) { var index = int.Parse(direction.Groups[1].Value, CultureInfo.InvariantCulture); rays.TryGetValue(index, out var ray); ray.Cartesian = Vector(direction.Groups[2].Value, number, $"Direction {index}"); rays[index] = ray; }
            if (!line.TrimStart().StartsWith("LASER_SOURCE_FORMAT_VERSION:", StringComparison.OrdinalIgnoreCase) && !line.TrimStart().StartsWith("SPHERICAL_", StringComparison.OrdinalIgnoreCase)) canonical.AppendLine(line);
        }
        foreach (var (index, ray) in rays) { if (ray.Cartesian is null || ray.Spherical is null) throw new FormatException($"Ray {index} must contain exactly one Position {index}, Direction {index}, and Direction_Spherical {index}."); if (ray.Cartesian.Value.LengthSquared() <= 0) throw new FormatException($"Invalid Direction {index}: direction vector must be non-zero."); var reconstructed = FromSpherical(ray.Spherical.Value.Azimuth, ray.Spherical.Value.Elevation); if (Vector3.Distance(Vector3.Normalize(ray.Cartesian.Value), reconstructed) > DirectionTolerance) throw new FormatException($"Line {ray.SphericalLine}: Direction_Spherical {index} does not match Direction {index}."); }
        return canonical.ToString();
    }
    private static (float Azimuth, float Elevation) ToSpherical(Vector3 direction) { var xy = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y); return (xy < PoleTolerance ? 0f : MathF.Atan2(direction.Y, direction.X), MathF.Atan2(direction.Z, xy)); }
    private static Vector3 FromSpherical(float azimuth, float elevation) => Vector3.Normalize(new Vector3(MathF.Cos(elevation) * MathF.Cos(azimuth), MathF.Cos(elevation) * MathF.Sin(azimuth), MathF.Sin(elevation)));
    private static Vector3 Vector(string text, int line, string field) { var values = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); if (values.Length != 3) throw new FormatException($"Line {line}: {field} must contain three numbers."); return new Vector3(Number(values[0], line, field), Number(values[1], line, field), Number(values[2], line, field)); }
    private static float Number(string text, int line, string field) { if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value)) throw new FormatException($"Line {line}: invalid finite number for {field}."); return value; }
}
