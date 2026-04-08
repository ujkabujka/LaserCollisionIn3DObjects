using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public sealed class IntegerEditorControl : NumericEditorBase
{
    public IntegerEditorControl(ObservableObject target, InspectableMemberMetadata member) : base(target, member, 1)
    {
    }

    protected override void Offset(double delta)
    {
        var current = Convert.ToInt32(CurrentValue ?? 0);
        SetValue(current + Convert.ToInt32(delta));
    }

    protected override void Commit()
    {
        if (IsReadOnly)
        {
            return;
        }

        if (int.TryParse(CurrentText, out var value))
        {
            SetValue(value);
            return;
        }

        Refresh();
    }

    protected override bool IsValidInput(string text)
    {
        if (string.IsNullOrEmpty(text) || text == "-")
        {
            return true;
        }

        return int.TryParse(text, out _);
    }
}
