using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.WpfHost.Controls;

public sealed class StringEditorControl : UserControl
{
    private readonly ObservableObject _target;
    private readonly InspectableMemberMetadata _member;
    private readonly TextBox? _textBox;
    private readonly ComboBox? _comboBox;
    private bool _isUpdating;

    public StringEditorControl(ObservableObject target, InspectableMemberMetadata member)
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

        FrameworkElement editor;
        if (member.ValueSourceProperty is not null)
        {
            _comboBox = new ComboBox
            {
                Margin = new Thickness(0, 2, 0, 2),
                IsEnabled = !MemberAccess.IsReadOnly(member)
            };
            _comboBox.SelectionChanged += HandleComboSelectionChanged;
            editor = _comboBox;
        }
        else
        {
            _textBox = new TextBox
            {
                Margin = new Thickness(0, 2, 0, 2),
                IsReadOnly = MemberAccess.IsReadOnly(member)
            };
            _textBox.LostFocus += (_, _) => CommitText();
            FocusHelpers.AttachEnterToCommitAndMoveFocus(_textBox, CommitText);
            editor = _textBox;
        }

        Grid.SetColumn(label, 0);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(label);
        grid.Children.Add(editor);

        Content = grid;

        _target.PropertyChanged += HandleTargetPropertyChanged;
        Unloaded += (_, _) => _target.PropertyChanged -= HandleTargetPropertyChanged;

        if (_comboBox is not null)
        {
            RefreshComboItems();
        }
        else
        {
            UpdateTextValue();
        }
    }

    private void HandleComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _comboBox?.SelectedItem is not string selected || _member.Property is null || !_member.Property.CanWrite)
        {
            return;
        }

        _member.Property.SetValue(_target, selected);
    }

    private void CommitText()
    {
        if (_textBox is null || _member.Property is null || !_member.Property.CanWrite)
        {
            return;
        }

        _member.Property.SetValue(_target, _textBox.Text);
    }

    private void HandleTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (_member.Property is not null && string.Equals(e.PropertyName, _member.Property.Name, StringComparison.Ordinal))
        {
            Dispatcher.Invoke(UpdateCurrentValue);
        }

        if (_member.ValueSourceProperty is not null && string.Equals(e.PropertyName, _member.ValueSourceProperty.Name, StringComparison.Ordinal))
        {
            Dispatcher.Invoke(RefreshComboItems);
        }
    }

    private void UpdateCurrentValue()
    {
        if (_comboBox is not null)
        {
            UpdateComboSelection();
        }
        else
        {
            UpdateTextValue();
        }
    }

    private void UpdateTextValue()
    {
        if (_textBox is null)
        {
            return;
        }

        var value = _member.Property?.GetValue(_target)?.ToString() ?? string.Empty;
        if (string.Equals(_textBox.Text, value, StringComparison.Ordinal))
        {
            return;
        }

        _isUpdating = true;
        _textBox.Text = value;
        _isUpdating = false;
    }

    private void RefreshComboItems()
    {
        if (_comboBox is null)
        {
            return;
        }

        var options = GetSourceValues();
        var current = GetCurrentValue();
        if (!string.IsNullOrWhiteSpace(current) && !options.Contains(current, StringComparer.Ordinal))
        {
            options.Insert(0, current);
        }

        _isUpdating = true;
        _comboBox.ItemsSource = options;
        _comboBox.SelectedItem = options.FirstOrDefault(o => string.Equals(o, current, StringComparison.Ordinal)) ?? current;
        _isUpdating = false;
    }

    private void UpdateComboSelection()
    {
        if (_comboBox is null)
        {
            return;
        }

        var current = GetCurrentValue();
        _isUpdating = true;
        _comboBox.SelectedItem = _comboBox.Items.OfType<string>().FirstOrDefault(item => string.Equals(item, current, StringComparison.Ordinal)) ?? current;
        _isUpdating = false;
    }

    private string GetCurrentValue()
        => _member.Property?.GetValue(_target)?.ToString() ?? string.Empty;

    private List<string> GetSourceValues()
    {
        if (_member.ValueSourceProperty?.GetValue(_target) is not IEnumerable enumerable)
        {
            return new List<string>();
        }

        var values = enumerable
            .OfType<object?>()
            .Select(value => value?.ToString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return values;
    }
}
