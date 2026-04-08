using System.ComponentModel;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Notes;

namespace BaseFramework.Wpf.Controls;

public sealed class NoteEditorControl : UserControl
{
    private readonly ObservableObject _target;
    private readonly InspectableMemberMetadata _member;
    private readonly TextBox _noteBox;
    private bool _isUpdating;

    public NoteEditorControl(ObservableObject target, InspectableMemberMetadata member)
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

        _noteBox = new TextBox
        {
            MinHeight = 120,
            Margin = new Thickness(0, 4, 0, 4),
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            IsReadOnly = MemberAccess.IsReadOnly(member)
        };
        _noteBox.LostFocus += (_, _) => Commit();

        Grid.SetColumn(label, 0);
        Grid.SetColumn(_noteBox, 1);

        grid.Children.Add(label);
        grid.Children.Add(_noteBox);
        Content = grid;

        _target.PropertyChanged += HandleTargetPropertyChanged;
        Unloaded += (_, _) => _target.PropertyChanged -= HandleTargetPropertyChanged;

        Loaded += (_, _) => Refresh();
    }

    private void HandleTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (!MemberAccess.MatchesPropertyChange(_member, e.PropertyName))
        {
            return;
        }

        Dispatcher.Invoke(Refresh);
    }

    private void Refresh()
    {
        var note = MemberAccess.GetValue(_member, _target) as NoteDocument ?? new NoteDocument();
        _isUpdating = true;
        _noteBox.Text = note.Text;
        _isUpdating = false;
    }

    private void Commit()
    {
        if (_isUpdating || MemberAccess.IsReadOnly(_member))
        {
            return;
        }

        var text = _noteBox.Text?.TrimEnd('\r', '\n') ?? string.Empty;

        var current = MemberAccess.GetValue(_member, _target) as NoteDocument;
        if (current is not null && string.Equals(current.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        var updated = new NoteDocument(text);
        MemberAccess.SetValue(_member, _target, updated);
    }
}

