namespace BaseFramework.Core.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = true)]
public sealed class InspectableEditorAttribute(string hint) : Attribute
{
    public string Hint { get; } = hint;
}
