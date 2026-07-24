using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using LaserCollisionIn3DObjects.Domain.Import;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;

public sealed class ViaAnnotationLoader
{
    public AnnotationProject LoadFromFolder(string folderPath)
    {
        var jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
        if (jsonFiles.Length == 0) throw new InvalidOperationException("No JSON annotation file was found in the selected folder.");
        Exception? lastError = null;
        foreach (var jsonFile in jsonFiles)
        {
            try { return LoadFromJson(jsonFile, folderPath); }
            catch (Exception ex) { lastError = ex; }
        }
        throw new InvalidOperationException($"No valid VIA JSON file was found. Last error: {lastError?.Message}");
    }

    private static AnnotationProject LoadFromJson(string jsonPath, string folderPath)
    {
        using var stream = File.OpenRead(jsonPath);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var imagesNode = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("_via_img_metadata", out var metadata) ? metadata : root;
        if (imagesNode.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Unsupported VIA JSON root format.");
        var project = new AnnotationProject { JsonFilePath = jsonPath };
        foreach (var item in imagesNode.EnumerateObject()) project.Images.Add(ParseImageRecord(item.Name, item.Value, folderPath));
        return project;
    }

    private static AnnotatedImageRecord ParseImageRecord(string key, JsonElement imageElement, string folderPath)
    {
        var fileName = imageElement.TryGetProperty("filename", out var fileNameElement) ? fileNameElement.GetString() ?? string.Empty : string.Empty;
        var record = new AnnotatedImageRecord { Key = key, FileName = fileName };
        if (string.IsNullOrWhiteSpace(fileName)) record.Diagnostics.Add("Missing filename in VIA image record.");
        else { record.ImagePath = Path.Combine(folderPath, fileName); if (!File.Exists(record.ImagePath)) record.Diagnostics.Add($"Image file not found on disk: {record.ImagePath}"); }
        if (!imageElement.TryGetProperty("regions", out var regionsElement)) { record.Diagnostics.Add("No regions node was found."); return record; }
        var regions = ReadRegions(regionsElement, record.Diagnostics);
        if (regions.Count == 0) record.Diagnostics.Add("No valid regions were parsed for this image.");

        var panelRegions = regions.Where(static r => AnnotationTypeClassifier.IsPanel(r.Type)).ToList();
        if (panelRegions.Count == 1)
        {
            if (panelRegions[0].Shape is PolygonShapeData polygon && polygon.Points.Count >= 3) record.Panel = new PanelAnnotation { OriginalPolygonPoints = polygon.Points };
            else record.Diagnostics.Add($"Panel region must be a polygon with at least 3 points (found '{panelRegions[0].ShapeName}').");
        }
        else if (panelRegions.Count == 0) record.Diagnostics.Add("No panel region found (type must be panel, plane, or 3).");
        else record.Diagnostics.Add($"Expected one panel region but found {panelRegions.Count}.");

        foreach (var collisionPoint in regions.Where(static r => !AnnotationTypeClassifier.IsPanel(r.Type)))
        {
            switch (collisionPoint.Shape)
            {
                case PolygonShapeData polygon: AddHole(record, AnnotationShapeType.Polygon, polygon, GeometryUtilities.PolygonCentroid(polygon.Points), GeometryUtilities.PolygonArea(polygon.Points)); break;
                case CircleShapeData circle: AddHole(record, AnnotationShapeType.Circle, circle, circle.Center, GeometryUtilities.CircleArea(circle.Radius)); break;
                case EllipseShapeData ellipse: AddHole(record, AnnotationShapeType.Ellipse, ellipse, ellipse.Center, GeometryUtilities.EllipseArea(ellipse.RadiusX, ellipse.RadiusY)); break;
                default: record.Diagnostics.Add($"Skipped collision-point region of unsupported shape '{collisionPoint.ShapeName}' (type '{collisionPoint.Type}')."); break;
            }
        }
        return record;
    }

    private static void AddHole(AnnotatedImageRecord record, AnnotationShapeType shapeType, IAnnotationShape shape, Point center, double area) => record.Holes.Add(new HoleAnnotation { ShapeType = shapeType, OriginalShape = shape, CenterPoint = center, PixelArea = area });

    private static List<RegionRecord> ReadRegions(JsonElement regions, ICollection<string> diagnostics)
    {
        var list = new List<RegionRecord>();
        if (regions.ValueKind == JsonValueKind.Array)
        {
            foreach (var region in regions.EnumerateArray()) if (TryParseRegion(region, out var parsed, diagnostics)) list.Add(parsed);
        }
        else if (regions.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in regions.EnumerateObject()) if (TryParseRegion(property.Value, out var parsed, diagnostics)) list.Add(parsed);
        }
        return list;
    }

    private static bool TryParseRegion(JsonElement regionElement, out RegionRecord region, ICollection<string> diagnostics)
    {
        region = default;
        if (!regionElement.TryGetProperty("shape_attributes", out var attributes) || !attributes.TryGetProperty("name", out var nameElement)) return false;
        var type = ReadType(regionElement);
        var shapeName = nameElement.ValueKind == JsonValueKind.String ? nameElement.GetString() ?? string.Empty : nameElement.ToString();
        if (!TryParseShape(shapeName, attributes, out var shape, out var diagnostic) && diagnostic is not null) diagnostics.Add(diagnostic);
        region = new RegionRecord(type, shapeName, shape);
        return true;
    }

    private static string ReadType(JsonElement regionElement)
    {
        if (!regionElement.TryGetProperty("region_attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Object || !attributes.TryGetProperty("type", out var type)) return string.Empty;
        return type.ValueKind switch { JsonValueKind.String => type.GetString() ?? string.Empty, JsonValueKind.Number => type.GetRawText(), _ => type.ToString() };
    }

    private static bool TryParseShape(string name, JsonElement shape, out IAnnotationShape? parsed, out string? diagnostic)
    {
        parsed = null; diagnostic = null;
        if (string.Equals(name, "rect", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadDouble(shape, "x", out var x) || !double.IsFinite(x)) { diagnostic = "Skipped malformed rectangle region: x is missing or invalid."; return false; }
            if (!TryReadDouble(shape, "y", out var y) || !double.IsFinite(y)) { diagnostic = "Skipped malformed rectangle region: y is missing or invalid."; return false; }
            if (!TryReadDouble(shape, "width", out var width) || !double.IsFinite(width) || width <= 0) { diagnostic = "Skipped malformed rectangle region: width must be greater than zero."; return false; }
            if (!TryReadDouble(shape, "height", out var height) || !double.IsFinite(height) || height <= 0) { diagnostic = "Skipped malformed rectangle region: height must be greater than zero."; return false; }
            parsed = new PolygonShapeData { Points = [new Point(x, y), new Point(x + width, y), new Point(x + width, y + height), new Point(x, y + height)] }; return true;
        }
        if (string.Equals(name, "polygon", StringComparison.OrdinalIgnoreCase) && shape.TryGetProperty("all_points_x", out var xs) && shape.TryGetProperty("all_points_y", out var ys) && xs.ValueKind == JsonValueKind.Array && ys.ValueKind == JsonValueKind.Array)
        { try { parsed = new PolygonShapeData { Points = xs.EnumerateArray().Zip(ys.EnumerateArray(), (x, y) => new Point(x.GetDouble(), y.GetDouble())).ToArray() }; return true; } catch (FormatException) { return false; } }
        if (string.Equals(name, "circle", StringComparison.OrdinalIgnoreCase) && TryReadDouble(shape, "cx", out var cx) && TryReadDouble(shape, "cy", out var cy) && TryReadDouble(shape, "r", out var r)) { parsed = new CircleShapeData { Center = new Point(cx, cy), Radius = r }; return true; }
        if (string.Equals(name, "ellipse", StringComparison.OrdinalIgnoreCase) && TryReadDouble(shape, "cx", out var ecx) && TryReadDouble(shape, "cy", out var ecy) && TryReadDouble(shape, "rx", out var rx) && TryReadDouble(shape, "ry", out var ry)) { parsed = new EllipseShapeData { Center = new Point(ecx, ecy), RadiusX = rx, RadiusY = ry }; return true; }
        return false;
    }

    private static bool TryReadDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.Number ? property.TryGetDouble(out value) : double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value));
    }

    private sealed record RegionRecord(string Type, string ShapeName, IAnnotationShape? Shape);
}
