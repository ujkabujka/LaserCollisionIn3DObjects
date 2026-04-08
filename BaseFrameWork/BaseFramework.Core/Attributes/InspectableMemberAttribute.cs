namespace BaseFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class InspectableMemberAttribute : Attribute
{
    public InspectableMemberAttribute(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public bool ReadOnly { get; init; }
    public int Order { get; init; }
    public string? ValueSourcePropertyName { get; init; }
}
