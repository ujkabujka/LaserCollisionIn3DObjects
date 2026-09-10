using System.Globalization;
using System.Text.RegularExpressions;

namespace LaserCollisionIn3DObjects.Domain.Import;

public readonly record struct AnnotationImageIdentity(int TestNumber, int PanelNumber)
{
    private static readonly Regex FileNamePattern = new(
        "^t(?<test>\\d+)p(?<panel>\\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryParse(string? fileName, out AnnotationImageIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var match = FileNamePattern.Match(Path.GetFileNameWithoutExtension(fileName));
        if (!match.Success
            || !int.TryParse(match.Groups["test"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var testNumber)
            || !int.TryParse(match.Groups["panel"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var panelNumber))
        {
            return false;
        }

        identity = new AnnotationImageIdentity(testNumber, panelNumber);
        return true;
    }
}

/// <summary>
/// Orders t{test}p{panel} names numerically. Matching names precede other names,
/// which use the application's existing natural filename ordering as a stable fallback.
/// </summary>
public sealed class AnnotationImageFileNameComparer : IComparer<string>
{
    public static AnnotationImageFileNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var xMatches = AnnotationImageIdentity.TryParse(x, out var xIdentity);
        var yMatches = AnnotationImageIdentity.TryParse(y, out var yIdentity);
        if (xMatches != yMatches) return xMatches ? -1 : 1;
        if (!xMatches) return NaturalFileNameComparer.Instance.Compare(x, y);

        var result = xIdentity.TestNumber.CompareTo(yIdentity.TestNumber);
        if (result != 0) return result;
        result = xIdentity.PanelNumber.CompareTo(yIdentity.PanelNumber);
        return result != 0 ? result : NaturalFileNameComparer.Instance.Compare(x, y);
    }
}
