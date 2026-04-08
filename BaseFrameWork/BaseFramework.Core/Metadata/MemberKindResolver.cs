using System.Collections;
using BaseFramework.Core.Notes;

namespace BaseFramework.Core.Metadata;

public static class MemberKindResolver
{
    public static MemberKind Resolve(Type type, string? editorHint = null, bool hasValueSource = false)
    {
        if (string.Equals(editorHint, EditorHints.Selection, StringComparison.OrdinalIgnoreCase) || hasValueSource)
        {
            return MemberKind.Selection;
        }

        if (string.Equals(editorHint, EditorHints.Multiline, StringComparison.OrdinalIgnoreCase))
        {
            return MemberKind.MultiLineText;
        }

        if (string.Equals(editorHint, EditorHints.File, StringComparison.OrdinalIgnoreCase))
        {
            return MemberKind.File;
        }

        if (string.Equals(editorHint, EditorHints.Image, StringComparison.OrdinalIgnoreCase))
        {
            return MemberKind.Image;
        }

        if (string.Equals(editorHint, EditorHints.Table, StringComparison.OrdinalIgnoreCase))
        {
            return MemberKind.Table;
        }

        if (type.IsEnum) return MemberKind.Enum;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return MemberKind.Integer;
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)) return MemberKind.Double;
        if (type == typeof(string)) return MemberKind.String;
        if (type == typeof(NoteDocument)) return MemberKind.Note;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return MemberKind.DateTime;
        if (type == typeof(bool)) return MemberKind.Boolean;
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string)) return MemberKind.Collection;
        if (!type.IsPrimitive && !type.IsValueType && type != typeof(string)) return MemberKind.Class;

        return MemberKind.Unknown;
    }
}
