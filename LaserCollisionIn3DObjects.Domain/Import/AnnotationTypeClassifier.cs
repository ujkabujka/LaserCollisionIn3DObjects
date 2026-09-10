namespace LaserCollisionIn3DObjects.Domain.Import;

public static class AnnotationTypeClassifier
{
    public static AnnotationSemanticType Classify(string? type) => type?.Trim() switch
    {
        "1" => AnnotationSemanticType.Hole,
        "2" => AnnotationSemanticType.Natural,
        "3" => AnnotationSemanticType.Panel,
        string value when value.Equals("panel", StringComparison.OrdinalIgnoreCase) => AnnotationSemanticType.Panel,
        string value when value.Equals("plane", StringComparison.OrdinalIgnoreCase) => AnnotationSemanticType.Panel,
        _ => AnnotationSemanticType.Unknown,
    };

    public static bool IsHole(string? type) => Classify(type) == AnnotationSemanticType.Hole;
    public static bool IsNatural(string? type) => Classify(type) == AnnotationSemanticType.Natural;
    /// <summary>Returns true only for VIA panel aliases: panel, plane, and numeric type 3.</summary>
    public static bool IsPanel(string? type) => Classify(type) == AnnotationSemanticType.Panel;
}

public enum AnnotationSemanticType { Unknown, Hole, Natural, Panel }
