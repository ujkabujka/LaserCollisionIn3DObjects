namespace BaseFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class InspectableValidationAttribute : Attribute
{
    public bool Required { get; init; }
    public double Minimum { get; init; } = double.NaN;
    public double Maximum { get; init; } = double.NaN;
    public string? RegexPattern { get; init; }
}
