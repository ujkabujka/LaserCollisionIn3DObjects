using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

internal static class MemberAccess
{
    public static bool CanWrite(InspectableMemberMetadata member)
        => member.Property is not null && member.Property.CanWrite && !member.ReadOnly;

    public static bool IsReadOnly(InspectableMemberMetadata member)
        => !CanWrite(member);
}
