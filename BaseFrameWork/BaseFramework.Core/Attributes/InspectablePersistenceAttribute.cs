namespace BaseFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class InspectablePersistenceAttribute(string persistenceKey) : Attribute
{
    public string PersistenceKey { get; } = persistenceKey;
    public string? DatabaseKey { get; init; }
}
