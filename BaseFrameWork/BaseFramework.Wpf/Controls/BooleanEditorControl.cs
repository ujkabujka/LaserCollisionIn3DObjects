using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.Wpf.Controls;

public sealed class BooleanEditorControl : UserControl
{
    public BooleanEditorControl(ObservableObject target, InspectableMemberMetadata member)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var canWrite = MemberAccess.CanWrite(member);
        var label = new TextBlock { Text = member.DisplayName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        var check = new CheckBox { IsChecked = (bool?)MemberAccess.GetValue(member, target), VerticalAlignment = VerticalAlignment.Center, IsEnabled = canWrite };
        check.Checked += (_, _) =>
        {
            if (canWrite)
            {
                MemberAccess.SetValue(member, target, true);
            }
        };
        check.Unchecked += (_, _) =>
        {
            if (canWrite)
            {
                MemberAccess.SetValue(member, target, false);
            }
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(check, 1);
        grid.Children.Add(label);
        grid.Children.Add(check);
        Content = grid;

        target.PropertyChanged += (_, args) =>
        {
            if (MemberAccess.MatchesPropertyChange(member, args.PropertyName))
            {
                Dispatcher.Invoke(() => check.IsChecked = (bool?)MemberAccess.GetValue(member, target));
            }
        };
    }
}

