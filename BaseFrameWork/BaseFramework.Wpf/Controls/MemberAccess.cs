using BaseFramework.Core.Metadata;

namespace BaseFramework.Wpf.Controls;

internal static class MemberAccess
{
    public static bool CanWrite(InspectableMemberMetadata member)
        => member.Setter is not null && !member.ReadOnly && member.EffectiveAccess.CanEdit;

    public static bool IsReadOnly(InspectableMemberMetadata member)
        => !CanWrite(member);

    public static bool CanInvoke(InspectableMemberMetadata member)
        => member.Invoker is not null && member.EffectiveAccess.CanInvoke;

    public static object? GetValue(InspectableMemberMetadata member, object target)
        => member.Getter is not null
            ? member.Getter(target, member)
            : member.Property?.GetValue(target);

    public static void SetValue(InspectableMemberMetadata member, object target, object? value)
    {
        if (!CanWrite(member))
        {
            return;
        }

        if (member.Setter is not null)
        {
            member.Setter(target, value, member);
            return;
        }

        if (member.Property is not null && member.Property.CanWrite)
        {
            member.Property.SetValue(target, value);
        }
    }

    public static object? Invoke(InspectableMemberMetadata member, object target, IReadOnlyList<object?> values)
        => member.Invoker is not null
            ? member.Invoker(target, values, member)
            : member.Method?.Invoke(target, values.ToArray());

    public static System.Collections.IEnumerable? GetValueSource(InspectableMemberMetadata member, object target)
        => member.ValueSourceAccessor is not null
            ? member.ValueSourceAccessor(target, member)
            : member.ValueSourceProperty?.GetValue(target) as System.Collections.IEnumerable;

    public static bool MatchesPropertyChange(InspectableMemberMetadata member, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return string.Equals(propertyName, member.ClrName, StringComparison.Ordinal)
            || string.Equals(propertyName, member.Key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(propertyName, member.Property?.Name, StringComparison.Ordinal);
    }

    public static bool MatchesValueSourceChange(InspectableMemberMetadata member, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return string.Equals(propertyName, member.ValueSourceProperty?.Name, StringComparison.Ordinal);
    }
}
