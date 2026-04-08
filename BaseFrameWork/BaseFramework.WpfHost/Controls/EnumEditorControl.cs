using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public sealed class EnumEditorControl : UserControl
{
    public EnumEditorControl(ObservableObject target, InspectableMemberMetadata member)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var canWrite = MemberAccess.CanWrite(member);
        var label = new TextBlock { Text = member.DisplayName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        var combo = new ComboBox { ItemsSource = Enum.GetValues(member.ValueType), SelectedItem = member.Property?.GetValue(target), IsEnabled = canWrite };
        combo.SelectionChanged += (_, _) =>
        {
            if (canWrite)
            {
                member.Property!.SetValue(target, combo.SelectedItem);
            }
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(combo, 1);
        grid.Children.Add(label);
        grid.Children.Add(combo);

        Content = grid;
    }
}
