using System.Globalization;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public sealed class DateTimeEditorControl : UserControl
{
    private readonly ObservableObject _target;
    private readonly InspectableMemberMetadata _member;
    private readonly DatePicker _datePicker;
    private readonly TextBox _timeBox;
    private bool _isUpdating;

    public DateTimeEditorControl(ObservableObject target, InspectableMemberMetadata member)
    {
        _target = target;
        _member = member;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = member.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 8, 4)
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };

        _datePicker = new DatePicker { Width = 130, Margin = new Thickness(0, 2, 4, 2), IsEnabled = !MemberAccess.IsReadOnly(member) };
        _datePicker.SelectedDateChanged += (_, _) => Commit();

        _timeBox = new TextBox { Width = 70, Margin = new Thickness(0, 2, 0, 2), IsReadOnly = MemberAccess.IsReadOnly(member) };
        _timeBox.PreviewTextInput += (_, e) => InputTextGuards.BlockInvalidTyping(_timeBox, e, IsTimeInputValid);
        DataObject.AddPastingHandler(_timeBox, (_, e) => InputTextGuards.BlockInvalidPaste(_timeBox, e, IsTimeInputValid));
        _timeBox.LostFocus += (_, _) => Commit();
        FocusHelpers.AttachEnterToCommitAndMoveFocus(_timeBox, Commit);

        stack.Children.Add(_datePicker);
        stack.Children.Add(_timeBox);

        Grid.SetColumn(label, 0);
        Grid.SetColumn(stack, 1);
        grid.Children.Add(label);
        grid.Children.Add(stack);

        Content = grid;

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

        Loaded += (_, _) => Refresh();
    }

    private void Commit()
    {
        if (_isUpdating || MemberAccess.IsReadOnly(_member) || _member.Property is null || !_datePicker.SelectedDate.HasValue)
        {
            return;
        }

        if (!TryParseTime(_timeBox.Text, out var time))
        {
            Refresh();
            return;
        }

        var date = _datePicker.SelectedDate.Value.Date;
        var candidateTicks = date.Ticks + time.Ticks;
        if (candidateTicks < DateTime.MinValue.Ticks || candidateTicks > DateTime.MaxValue.Ticks)
        {
            Refresh();
            return;
        }

        var value = new DateTime(candidateTicks, date.Kind);
        _member.Property.SetValue(_target, value);
    }

    private static bool IsTimeInputValid(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (text.Length > 5)
        {
            return false;
        }

        var parts = text.Split(':');
        if (parts.Length > 2)
        {
            return false;
        }

        if (parts.Any(part => part.Length > 2 || part.Any(ch => !char.IsDigit(ch))))
        {
            return false;
        }

        if (parts.Length == 2)
        {
            if (parts[0].Length == 2 && int.Parse(parts[0], CultureInfo.InvariantCulture) > 23)
            {
                return false;
            }

            if (parts[1].Length == 2 && int.Parse(parts[1], CultureInfo.InvariantCulture) > 59)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            time = TimeSpan.Zero;
            return true;
        }

        if (TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time))
        {
            return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
        }

        return false;
    }

    private void Refresh()
    {
        if (_member.Property?.GetValue(_target) is not DateTime value)
        {
            value = DateTime.Now;
        }

        _isUpdating = true;
        _datePicker.SelectedDate = value.Date;
        _timeBox.Text = value.ToString("HH:mm", CultureInfo.InvariantCulture);
        _isUpdating = false;
    }
}
