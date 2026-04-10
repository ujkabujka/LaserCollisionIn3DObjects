using System.IO;
using System.Text.Json;
using System.Windows;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Geometry;
using LaserCollisionIn3DObjects.Wpf.Features.Annotations.Models;

namespace LaserCollisionIn3DObjects.Wpf.Features.Annotations.Services;

public sealed class ViaAnnotationLoader
{
    public AnnotationProject LoadFromFolder(string folderPath)
    {
        var jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.TopDirectoryOnly);
        if (jsonFiles.Length == 0)
        {
            throw new InvalidOperationException("No JSON annotation file was found in the selected folder.");
        }

        Exception? lastError = null;
        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                return LoadFromJson(jsonFile, folderPath);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"No valid VIA JSON file was found. Last error: {lastError?.Message}");
    }

    private static AnnotationProject LoadFromJson(string jsonPath, string folderPath)
    {
        using var stream = File.OpenRead(jsonPath);
        using var document = JsonDocument.Parse(stream);

        var root = document.RootElement;
        var imagesNode = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("_via_img_metadata", out var metadata)
            ? metadata
            : root;

        if (imagesNode.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Unsupported VIA JSON root format.");
        }

        var project = new AnnotationProject { JsonFilePath = jsonPath };
        foreach (var item in imagesNode.EnumerateObject())
        {
            var record = ParseImageRecord(item.Name, item.Value, folderPath);
            project.Images.Add(record);
        }

        return project;
    }

    private static AnnotatedImageRecord ParseImageRecord(string key, JsonElement imageElement, string folderPath)
    {
        var fileName = imageElement.TryGetProperty("filename", out var fileNameElement)
            ? fileNameElement.GetString() ?? string.Empty
            : string.Empty;

        var record = new AnnotatedImageRecord { Key = key, FileName = fileName };
        if (string.IsNullOrWhiteSpace(fileName))
        {
            record.Diagnostics.Add("Missing filename in VIA image record.");
        }
        else
        {
            record.ImagePath = Path.Combine(folderPath, fileName);
            if (!File.Exists(record.ImagePath))
            {
                record.Diagnostics.Add($"Image file not found on disk: {record.ImagePath}");
            }
        }

        if (!imageElement.TryGetProperty("regions", out var regionsElement))
        {
            record.Diagnostics.Add("No regions node was found.");
            return record;
        }

        var regions = ReadRegions(regionsElement);
        if (regions.Count == 0)
        {
            record.Diagnostics.Add("No valid regions were parsed for this image.");
        }

        var panelRegions = regions.Where(static r => string.Equals(r.Type, "panel", StringComparison.OrdinalIgnoreCase)).ToList();
        var holeRegions = regions.Where(static r => string.Equals(r.Type, "hole", StringComparison.OrdinalIgnoreCase)).ToList();

        if (panelRegions.Count == 1)
        {
            var panelShape = panelRegions[0].Shape;
            if (panelShape is PolygonShapeData polygon && polygon.Points.Count >= 3)
            {
                record.Panel = new PanelAnnotation { OriginalPolygonPoints = polygon.Points };
            }
            else
            {
                record.Diagnostics.Add("Panel region is not a polygon with at least 3 points.");
            }
        }
        else if (panelRegions.Count == 0)
        {
            record.Diagnostics.Add("No panel region found (region_attributes.type == panel).");
        }
        else
        {
            record.Diagnostics.Add($"Expected one panel region but found {panelRegions.Count}.");
        }

        foreach (var hole in holeRegions)
        {
            switch (hole.Shape)
            {
                case PolygonShapeData polygon:
                    record.Holes.Add(new HoleAnnotation
                    {
                        ShapeType = AnnotationShapeType.Polygon,
                        OriginalShape = polygon,
                        CenterPoint = GeometryUtilities.PolygonCentroid(polygon.Points),
                        PixelArea = GeometryUtilities.PolygonArea(polygon.Points),
                    });
                    break;
                case CircleShapeData circle:
                    record.Holes.Add(new HoleAnnotation
                    {
                        ShapeType = AnnotationShapeType.Circle,
                        OriginalShape = circle,
                        CenterPoint = circle.Center,
                        PixelArea = GeometryUtilities.CircleArea(circle.Radius),
                    });
                    break;
                case EllipseShapeData ellipse:
                    record.Holes.Add(new HoleAnnotation
                    {
                        ShapeType = AnnotationShapeType.Ellipse,
                        OriginalShape = ellipse,
                        CenterPoint = ellipse.Center,
                        PixelArea = GeometryUtilities.EllipseArea(ellipse.RadiusX, ellipse.RadiusY),
                    });
                    break;
                default:
                    record.Diagnostics.Add("Unsupported hole shape type encountered.");
                    break;
            }
        }

        return record;
    }

    private static List<RegionRecord> ReadRegions(JsonElement regions)
    {
        var list = new List<RegionRecord>();

        if (regions.ValueKind == JsonValueKind.Array)
        {
            foreach (var region in regions.EnumerateArray())
            {
                if (TryParseRegion(region, out var parsed))
                {
                    list.Add(parsed);
                }
            }

            return list;
        }

        if (regions.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in regions.EnumerateObject())
            {
                if (TryParseRegion(property.Value, out var parsed))
                {
                    list.Add(parsed);
                }
            }
        }

        return list;
    }

    private static bool TryParseRegion(JsonElement regionElement, out RegionRecord region)
    {
        region = default;
        if (!regionElement.TryGetProperty("shape_attributes", out var shapeAttributes))
        {
            return false;
        }

        var type = "";
        if (regionElement.TryGetProperty("region_attributes", out var regionAttributes) &&
            regionAttributes.ValueKind == JsonValueKind.Object &&
            regionAttributes.TryGetProperty("type", out var typeElement))
        {
            type = typeElement.GetString() ?? string.Empty;
        }

        if (!shapeAttributes.TryGetProperty("name", out var nameElement))
        {
            return false;
        }

        var shapeName = nameElement.GetString() ?? string.Empty;
        if (!TryParseShape(shapeName, shapeAttributes, out var shape))
        {
            return false;
        }

        region = new RegionRecord(type, shape);
        return true;
    }

    private static bool TryParseShape(string shapeName, JsonElement shape, out IAnnotationShape parsed)
    {
        parsed = null!;
        if (string.Equals(shapeName, "polygon", StringComparison.OrdinalIgnoreCase) &&
            shape.TryGetProperty("all_points_x", out var xs) &&
            shape.TryGetProperty("all_points_y", out var ys) &&
            xs.ValueKind == JsonValueKind.Array &&
            ys.ValueKind == JsonValueKind.Array)
        {
            var points = xs.EnumerateArray().Zip(ys.EnumerateArray(), (x, y) => new Point(x.GetDouble(), y.GetDouble())).ToArray();
            parsed = new PolygonShapeData { Points = points };
            return true;
        }

        if (string.Equals(shapeName, "circle", StringComparison.OrdinalIgnoreCase) &&
            TryReadDouble(shape, "cx", out var cx) && TryReadDouble(shape, "cy", out var cy) && TryReadDouble(shape, "r", out var r))
        {
            parsed = new CircleShapeData { Center = new Point(cx, cy), Radius = r };
            return true;
        }

        if (string.Equals(shapeName, "ellipse", StringComparison.OrdinalIgnoreCase) &&
            TryReadDouble(shape, "cx", out var ecx) && TryReadDouble(shape, "cy", out var ecy) && TryReadDouble(shape, "rx", out var rx) && TryReadDouble(shape, "ry", out var ry))
        {
            parsed = new EllipseShapeData { Center = new Point(ecx, ecy), RadiusX = rx, RadiusY = ry };
            return true;
        }

        return false;
    }

    private static bool TryReadDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var p))
        {
            return false;
        }

        return p.ValueKind == JsonValueKind.Number
            ? p.TryGetDouble(out value)
            : double.TryParse(p.GetString(), out value);
    }

    private readonly record struct RegionRecord(string Type, IAnnotationShape Shape);
}
