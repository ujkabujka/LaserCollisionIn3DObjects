using System.Globalization;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public sealed class DoubleEditorControl : NumericEditorBase
{
    public DoubleEditorControl(ObservableObject target, InspectableMemberMetadata member) : base(target, member, 5)
    {
    }

    protected override void Offset(double delta)
    {
        var current = Convert.ToDouble(CurrentValue ?? 0d, CultureInfo.InvariantCulture);
        SetValue(current + delta);
    }

    protected override void Commit()
    {
        if (IsReadOnly)
        {
            return;
        }

        if (NumericParsing.TryParseFlexibleDouble(CurrentText, out var value))
        {
            SetValue(value);
            return;
        }

        Refresh();
    }

    protected override bool IsValidInput(string text)
    {
        if (string.IsNullOrEmpty(text) || text is "-" or "." or "-.")
        {
            return true;
        }

        return NumericParsing.TryParseFlexibleDouble(text, out _);
    }
}
