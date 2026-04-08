namespace BaseFramework.Core.Access;

public sealed record MemberAccessEvaluation(
    bool CanView,
    bool CanEdit,
    bool CanInvoke,
    string? Reason = null)
{
    public static MemberAccessEvaluation Allowed { get; } = new(true, true, true);
}
