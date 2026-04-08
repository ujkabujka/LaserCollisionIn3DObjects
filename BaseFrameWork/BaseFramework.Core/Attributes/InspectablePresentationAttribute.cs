namespace BaseFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class InspectablePresentationAttribute : Attribute
{
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Section { get; init; }
    public string? HelpText { get; init; }
}
