using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.Wpf.Controls;

public sealed class EnumEditorControl : UserControl
{
    public EnumEditorControl(ObservableObject target, InspectableMemberMetadata member)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var canWrite = MemberAccess.CanWrite(member);
        var label = new TextBlock { Text = member.DisplayName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        var combo = new ComboBox { ItemsSource = Enum.GetValues(member.ValueType), SelectedItem = MemberAccess.GetValue(member, target), IsEnabled = canWrite };
        combo.SelectionChanged += (_, _) =>
        {
            if (canWrite)
            {
                MemberAccess.SetValue(member, target, combo.SelectedItem);
            }
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(combo, 1);
        grid.Children.Add(label);
        grid.Children.Add(combo);

        Content = grid;

        target.PropertyChanged += (_, args) =>
        {
            if (MemberAccess.MatchesPropertyChange(member, args.PropertyName))
            {
                Dispatcher.Invoke(() => combo.SelectedItem = MemberAccess.GetValue(member, target));
            }
        };
    }
}

