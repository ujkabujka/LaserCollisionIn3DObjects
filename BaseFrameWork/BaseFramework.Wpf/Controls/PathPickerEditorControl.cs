using Microsoft.Win32;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;

namespace BaseFramework.Wpf.Controls;

public sealed class PathPickerEditorControl : UserControl
{
    private readonly ObservableObject _target;
    private readonly InspectableMemberMetadata _member;
    private readonly TextBox _pathBox;
    private readonly bool _imagesOnly;

    public PathPickerEditorControl(ObservableObject target, InspectableMemberMetadata member, bool imagesOnly)
    {
        _target = target;
        _member = member;
        _imagesOnly = imagesOnly;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = member.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 8, 4)
        };

        _pathBox = new TextBox
        {
            Margin = new Thickness(0, 2, 4, 2),
            IsReadOnly = MemberAccess.IsReadOnly(member)
        };
        _pathBox.LostFocus += (_, _) => Commit();
        FocusHelpers.AttachEnterToCommitAndMoveFocus(_pathBox, Commit);

        var browseButton = new Button
        {
            Content = "Browse",
            IsEnabled = MemberAccess.CanWrite(member)
        };
        browseButton.Click += (_, _) => Browse();

        Grid.SetColumn(label, 0);
        Grid.SetColumn(_pathBox, 1);
        Grid.SetColumn(browseButton, 2);
        grid.Children.Add(label);
        grid.Children.Add(_pathBox);
        grid.Children.Add(browseButton);
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

    private void Browse()
    {
        if (!MemberAccess.CanWrite(_member))
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = _imagesOnly
                ? "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
                : "All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _pathBox.Text = dialog.FileName;
            Commit();
        }
    }

    private void Commit()
    {
        if (!MemberAccess.CanWrite(_member))
        {
            return;
        }

        MemberAccess.SetValue(_member, _target, _pathBox.Text);
    }

    private void Refresh()
    {
        var value = MemberAccess.GetValue(_member, _target)?.ToString() ?? string.Empty;
        if (!string.Equals(_pathBox.Text, value, StringComparison.Ordinal))
        {
            _pathBox.Text = value;
        }
    }
}
