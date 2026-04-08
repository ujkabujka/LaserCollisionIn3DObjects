using System.Globalization;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public abstract class NumericEditorBase : UserControl
{
    private readonly ObservableObject _target;
    private readonly InspectableMemberMetadata _member;
    private readonly TextBox _textBox;

    protected NumericEditorBase(ObservableObject target, InspectableMemberMetadata member, double step)
    {
        _target = target;
        _member = member;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock { Text = member.DisplayName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        _textBox = new TextBox { Margin = new Thickness(0, 2, 4, 2), IsReadOnly = MemberAccess.IsReadOnly(member) };

        if (MemberAccess.IsReadOnly(member))
        {
            grid.ColumnDefinitions[2].Width = new GridLength(0);
        }
        else
        {
            _textBox.PreviewTextInput += (_, e) => InputTextGuards.BlockInvalidTyping(_textBox, e, IsValidInput);
            DataObject.AddPastingHandler(_textBox, (_, e) => InputTextGuards.BlockInvalidPaste(_textBox, e, IsValidInput));

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            var up = new Button { Content = $"+{step}" };
            up.Click += (_, _) => Offset(step);
            var down = new Button { Content = $"-{step}" };
            down.Click += (_, _) => Offset(-step);
            stack.Children.Add(up);
            stack.Children.Add(down);
            Grid.SetColumn(stack, 2);
            grid.Children.Add(stack);
        }

        Grid.SetColumn(label, 0);
        Grid.SetColumn(_textBox, 1);
        grid.Children.Add(label);
        grid.Children.Add(_textBox);

        Content = grid;
        Loaded += (_, _) => Refresh();
        _target.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == _member.Property?.Name)
            {
                if (Dispatcher.CheckAccess())
                {
                    Refresh();
                }
                else
                {
                    Dispatcher.Invoke(Refresh);
                }
            }
        };

        _textBox.LostFocus += (_, _) => Commit();
        FocusHelpers.AttachEnterToCommitAndMoveFocus(_textBox, Commit);
    }

    protected abstract void Offset(double delta);

    protected abstract void Commit();

    protected abstract bool IsValidInput(string text);

    protected object? CurrentValue => _member.Property?.GetValue(_target);

    protected bool IsReadOnly => MemberAccess.IsReadOnly(_member);

    protected void SetValue(object value)
    {
        if (_member.Property is null || IsReadOnly)
        {
            return;
        }

        _member.Property.SetValue(_target, value);
        Refresh();
    }

    protected void Refresh() => _textBox.Text = Convert.ToString(CurrentValue, CultureInfo.CurrentCulture);

    protected string CurrentText => _textBox.Text;
}
