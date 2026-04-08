namespace BaseFramework.Core.Access;

public sealed record InspectableAccessRules(
    IReadOnlyList<string> VisibleRoles,
    IReadOnlyList<string> VisiblePermissions,
    IReadOnlyList<string> EditableRoles,
    IReadOnlyList<string> EditablePermissions,
    IReadOnlyList<string> InvokeRoles,
    IReadOnlyList<string> InvokePermissions)
{
    public static InspectableAccessRules Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
}
