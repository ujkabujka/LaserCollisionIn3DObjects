namespace BaseFramework.Core.Metadata;

public sealed record InspectableValidationHints(
    bool Required,
    double? Minimum,
    double? Maximum,
    string? RegexPattern)
{
    public static InspectableValidationHints Empty { get; } = new(false, null, null, null);
}
