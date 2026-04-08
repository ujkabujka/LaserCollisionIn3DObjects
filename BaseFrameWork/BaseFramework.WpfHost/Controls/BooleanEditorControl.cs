using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public sealed class BooleanEditorControl : UserControl
{
    public BooleanEditorControl(ObservableObject target, InspectableMemberMetadata member)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var canWrite = MemberAccess.CanWrite(member);
        var label = new TextBlock { Text = member.DisplayName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        var check = new CheckBox { IsChecked = (bool?)member.Property?.GetValue(target), VerticalAlignment = VerticalAlignment.Center, IsEnabled = canWrite };
        check.Checked += (_, _) =>
        {
            if (canWrite)
            {
                member.Property!.SetValue(target, true);
            }
        };
        check.Unchecked += (_, _) =>
        {
            if (canWrite)
            {
                member.Property!.SetValue(target, false);
            }
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(check, 1);
        grid.Children.Add(label);
        grid.Children.Add(check);
        Content = grid;
    }
}
