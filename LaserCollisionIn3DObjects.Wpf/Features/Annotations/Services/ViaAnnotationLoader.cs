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
    // VIA coordinates are pixel coordinates. Two pixels tolerates ordinary hand-drawn closure jitter.
    private const double ContourTolerancePixels = 2d;

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
        var records = imagesNode.EnumerateObject()
            .Select(item => ParseImageRecord(item.Name, item.Value, folderPath))
            .OrderBy(static image => image.FileName, AnnotationImageFileNameComparer.Instance);
        project.Images.AddRange(records);
        return project;
    }

    private static AnnotatedImageRecord ParseImageRecord(string key, JsonElement imageElement, string folderPath)
    {
        var fileName = imageElement.TryGetProperty("filename", out var fileNameElement) ? fileNameElement.GetString() ?? string.Empty : string.Empty;
        AnnotationImageIdentity? identity = AnnotationImageIdentity.TryParse(fileName, out var parsedIdentity) ? parsedIdentity : null;
        var record = new AnnotatedImageRecord { Key = key, FileName = fileName, Identity = identity };
        if (string.IsNullOrWhiteSpace(fileName)) record.Diagnostics.Add("Missing filename in VIA image record.");
        else { record.ImagePath = Path.Combine(folderPath, fileName); if (!File.Exists(record.ImagePath)) record.Diagnostics.Add($"Image file not found on disk: {record.ImagePath}"); }
        if (!imageElement.TryGetProperty("regions", out var regionsElement)) { record.Diagnostics.Add("No regions node was found."); return record; }
        var regions = ReadRegions(regionsElement, record.Diagnostics);
        if (regions.Count == 0) record.Diagnostics.Add("No valid regions were parsed for this image.");

        var panelRegions = regions.Where(static r => AnnotationTypeClassifier.IsPanel(r.Type)).ToList();
        var uniquePanelContours = new List<IReadOnlyList<Point>>();
        foreach (var panelRegion in panelRegions)
        {
            if (!TryGetPanelContour(panelRegion, record.Diagnostics, out var contour)) continue;
            if (uniquePanelContours.Any(existing => ContoursEquivalent(existing, contour, ContourTolerancePixels)))
            {
                record.Diagnostics.Add("Ignored duplicate panel boundary contour.");
                continue;
            }
            uniquePanelContours.Add(contour);
        }

        if (uniquePanelContours.Count == 1) record.Panel = new PanelAnnotation { OriginalPolygonPoints = uniquePanelContours[0] };
        else if (uniquePanelContours.Count > 1) record.Diagnostics.Add("Multiple distinct valid panel boundaries were found.");
        else if (panelRegions.Count == 0) record.Diagnostics.Add("No panel region found (type must be panel, plane, or 3).");
        else record.Diagnostics.Add("No valid panel boundary was found.");

        foreach (var collisionPoint in regions.Where(static r => !AnnotationTypeClassifier.IsPanel(r.Type)))
        {
            var category = AnnotationTypeClassifier.Classify(collisionPoint.Type) switch
            {
                AnnotationSemanticType.Hole => AnnotationPointCategory.Hole,
                AnnotationSemanticType.Natural => AnnotationPointCategory.Natural,
                _ => (AnnotationPointCategory?)null,
            };
            if (category is null)
            {
                record.Diagnostics.Add($"Skipped region with unknown annotation type '{collisionPoint.Type}'. Expected 1 (Hole), 2 (Natural), or 3 (Panel).");
                continue;
            }
            switch (collisionPoint.Shape)
            {
                case PolygonShapeData polygon: AddPoint(record, category.Value, AnnotationShapeType.Polygon, polygon, GeometryUtilities.PolygonCentroid(polygon.Points), GeometryUtilities.PolygonArea(polygon.Points)); break;
                case CircleShapeData circle: AddPoint(record, category.Value, AnnotationShapeType.Circle, circle, circle.Center, GeometryUtilities.CircleArea(circle.Radius)); break;
                case EllipseShapeData ellipse: AddPoint(record, category.Value, AnnotationShapeType.Ellipse, ellipse, ellipse.Center, GeometryUtilities.EllipseArea(ellipse.RadiusX, ellipse.RadiusY)); break;
                default: record.Diagnostics.Add($"Skipped collision-point region of unsupported shape '{collisionPoint.ShapeName}' (type '{collisionPoint.Type}')."); break;
            }
        }
        return record;
    }

    private static bool TryGetPanelContour(RegionRecord region, ICollection<string> diagnostics, out IReadOnlyList<Point> contour)
    {
        contour = Array.Empty<Point>();
        if (region.Shape is not PolygonShapeData polygon)
        {
            diagnostics.Add($"Ignored panel-typed region of unsupported panel shape '{region.ShapeName}'.");
            return false;
        }

        var points = polygon.Points.Where(static point => double.IsFinite(point.X) && double.IsFinite(point.Y)).ToList();
        if (region.ShapeName.Equals("polyline", StringComparison.OrdinalIgnoreCase))
        {
            if (points.Count < 5 || Distance(points[0], points[^1]) > ContourTolerancePixels)
            {
                diagnostics.Add("Ignored malformed panel polyline: at least four boundary points and a closed contour within 2 pixels are required.");
                return false;
            }
            // Drop the repeated closing point; downstream fitting consumes the same polygon form as VIA polygons.
            points.RemoveAt(points.Count - 1);
        }
        else if (points.Count > 3 && Distance(points[0], points[^1]) <= ContourTolerancePixels)
        {
            // VIA polygons occur both with and without an explicit repeated closing vertex.
            points.RemoveAt(points.Count - 1);
        }

        if (points.Count < 3)
        {
            diagnostics.Add($"Ignored malformed panel {region.ShapeName}: at least three valid points are required.");
            return false;
        }

        contour = points;
        return true;
    }

    private static bool ContoursEquivalent(IReadOnlyList<Point> left, IReadOnlyList<Point> right, double tolerance)
    {
        if (left.Count != right.Count) return false;
        for (var start = 0; start < right.Count; start++)
        {
            if (Matches(left, right, start, 1, tolerance) || Matches(left, right, start, -1, tolerance)) return true;
        }
        return false;
    }

    private static bool Matches(IReadOnlyList<Point> left, IReadOnlyList<Point> right, int start, int direction, double tolerance)
    {
        for (var i = 0; i < left.Count; i++)
        {
            var index = (start + direction * i) % right.Count;
            if (index < 0) index += right.Count;
            if (Distance(left[i], right[index]) > tolerance) return false;
        }
        return true;
    }

    private static double Distance(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void AddPoint(AnnotatedImageRecord record, AnnotationPointCategory category, AnnotationShapeType shapeType, IAnnotationShape shape, Point center, double area) => record.Points.Add(new AnnotatedPointAnnotation { Category = category, ShapeType = shapeType, OriginalShape = shape, CenterPoint = center, PixelArea = area });

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
        region = null!;
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
        if ((string.Equals(name, "polygon", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "polyline", StringComparison.OrdinalIgnoreCase)) && shape.TryGetProperty("all_points_x", out var xs) && shape.TryGetProperty("all_points_y", out var ys) && xs.ValueKind == JsonValueKind.Array && ys.ValueKind == JsonValueKind.Array)
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
