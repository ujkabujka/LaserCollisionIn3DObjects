namespace BaseFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class InspectableAccessAttribute : Attribute
{
    public string[] VisibleRoles { get; init; } = Array.Empty<string>();
    public string[] VisiblePermissions { get; init; } = Array.Empty<string>();
    public string[] EditableRoles { get; init; } = Array.Empty<string>();
    public string[] EditablePermissions { get; init; } = Array.Empty<string>();
    public string[] InvokeRoles { get; init; } = Array.Empty<string>();
    public string[] InvokePermissions { get; init; } = Array.Empty<string>();
}
