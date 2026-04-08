using System.Collections;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.Wpf.Controls;

public sealed class TableEditorControl : UserControl
{
    private readonly ObservableObject _target;
    private readonly InspectableMemberMetadata _member;
    private readonly TextBox _textBox;

    public TableEditorControl(ObservableObject target, InspectableMemberMetadata member)
    {
        _target = target;
        _member = member;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = member.DisplayName,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 8, 4)
        };

        _textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 100,
            IsReadOnly = true
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(_textBox, 1);
        grid.Children.Add(label);
        grid.Children.Add(_textBox);
        Content = grid;

        Loaded += (_, _) => Refresh();
        _target.PropertyChanged += HandleTargetPropertyChanged;
        Unloaded += (_, _) => _target.PropertyChanged -= HandleTargetPropertyChanged;
    }

    private void HandleTargetPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (MemberAccess.MatchesPropertyChange(_member, e.PropertyName))
        {
            Dispatcher.Invoke(Refresh);
        }
    }

    private void Refresh()
    {
        var value = MemberAccess.GetValue(_member, _target);
        _textBox.Text = value switch
        {
            null => "No table data loaded.",
            string text => text,
            IEnumerable enumerable => string.Join(Environment.NewLine, enumerable.Cast<object?>().Select(item => item?.ToString() ?? "(null)")),
            _ => value?.ToString() ?? string.Empty
        };
    }
}
