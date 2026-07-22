namespace LaserCollisionIn3DObjects.Domain.Import;

public static class AnnotationTypeClassifier
{
    /// <summary>Returns true only for VIA panel aliases: panel, plane, and numeric type 3.</summary>
    public static bool IsPanel(string? type) => type?.Trim() is string normalized
        && (normalized.Equals("panel", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("plane", StringComparison.OrdinalIgnoreCase)
            || normalized == "3");
}
