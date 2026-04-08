namespace BaseFramework.Core.Access;

public sealed record InspectableAccessContext(
    string? UserName,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions)
{
    public static InspectableAccessContext Empty { get; } = new(
        null,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static InspectableAccessContext Create(
        string? userName,
        IEnumerable<string>? roles,
        IEnumerable<string>? permissions)
        => new(
            userName,
            new HashSet<string>(roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(permissions ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase));
}
