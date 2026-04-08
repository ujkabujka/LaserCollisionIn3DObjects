using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.WpfHost.Controls;

public sealed class CollectionEditorControl : UserControl
{
    private readonly ObservableObject _owner;
    private readonly InspectableMemberMetadata _member;
    private readonly IObjectMetadataProvider _provider;
    private readonly Border _contentBorder;
    private readonly StackPanel _itemsPanel;
    private IEnumerable? _currentCollection;
    private INotifyCollectionChanged? _collectionNotifier;

    public CollectionEditorControl(ObservableObject owner, InspectableMemberMetadata member, IObjectMetadataProvider provider)
    {
        _owner = owner;
        _member = member;
        _provider = provider;
        _itemsPanel = new StackPanel();

        var host = new StackPanel();

        _contentBorder = new Border
        {
            Margin = new Thickness(12, 4, 0, 4),
            Padding = new Thickness(8, 6, 6, 6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            BorderBrush = Brushes.Transparent,
            Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
            Visibility = Visibility.Collapsed,
            Child = _itemsPanel
        };

        var toggle = new Expander
        {
            Header = member.DisplayName,
            IsExpanded = false
        };

        toggle.Expanded += (_, _) =>
        {
            BindCollection(forceRebuild: true);
            _contentBorder.Visibility = Visibility.Visible;
            _contentBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(14, 99, 156));
        };

        toggle.Collapsed += (_, _) =>
        {
            _contentBorder.Visibility = Visibility.Collapsed;
            _contentBorder.BorderBrush = Brushes.Transparent;
        };

        _owner.PropertyChanged += HandleOwnerPropertyChanged;
        Unloaded += (_, _) =>
        {
            _owner.PropertyChanged -= HandleOwnerPropertyChanged;
            DetachCollection();
        };

        host.Children.Add(toggle);
        host.Children.Add(_contentBorder);
        Content = host;

        BindCollection();
    }

    private void HandleOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, _member.Property?.Name, StringComparison.Ordinal))
        {
            return;
        }

        Dispatcher.Invoke(() => BindCollection(forceRebuild: true));
    }

    private void BindCollection(bool forceRebuild = false)
    {
        var value = _member.Property?.GetValue(_owner) as IEnumerable;
        if (!forceRebuild && ReferenceEquals(value, _currentCollection))
        {
            return;
        }

        DetachCollection();
        _currentCollection = value;

        if (_currentCollection is INotifyCollectionChanged notifier)
        {
            _collectionNotifier = notifier;
            _collectionNotifier.CollectionChanged += HandleCollectionChanged;
        }

        RebuildItems();
    }

    private void DetachCollection()
    {
        if (_collectionNotifier is not null)
        {
            _collectionNotifier.CollectionChanged -= HandleCollectionChanged;
            _collectionNotifier = null;
        }

        _currentCollection = null;
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(RebuildItems);
    }

    private void RebuildItems()
    {
        _itemsPanel.Children.Clear();

        if (_currentCollection is null)
        {
            _itemsPanel.Children.Add(new TextBlock { Text = "Collection is empty.", Opacity = 0.6, Margin = new Thickness(0, 2, 0, 2) });
            return;
        }

        foreach (var item in _currentCollection)
        {
            _itemsPanel.Children.Add(CreateItemHost(item));
        }

        if (_itemsPanel.Children.Count == 0)
        {
            _itemsPanel.Children.Add(new TextBlock { Text = "Collection is empty.", Opacity = 0.6, Margin = new Thickness(0, 2, 0, 2) });
        }
    }

    private FrameworkElement CreateItemHost(object item)
    {
        if (item is ObservableObject nested)
        {
            var container = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            var editor = new ClassEditorControl(nested.GetType().Name, nested, _provider);
            container.Children.Add(editor);

            if (CanRemoveItems())
            {
                var removeButton = CreateRemoveButton(nested);
                container.Children.Add(removeButton);
            }

            return container;
        }

        var panel = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock { Text = item?.ToString() ?? "null", Margin = new Thickness(6, 2, 0, 2) };
        Grid.SetColumn(text, 0);
        panel.Children.Add(text);

        if (CanRemoveItems())
        {
            var button = BuildRemoveButton();
            button.Click += (_, _) => RemoveItem(item);
            Grid.SetColumn(button, 1);
            panel.Children.Add(button);
        }

        return panel;
    }

    private FrameworkElement CreateRemoveButton(object item)
    {
        var host = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0),
            Background = Brushes.Transparent
        };

        var button = BuildRemoveButton();
        button.Click += (_, _) => RemoveItem(item);

        host.Child = button;
        return host;
    }

    private static Button BuildRemoveButton()
        => new()
        {
            Content = "x",
            Width = 28,
            Height = 28,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.OrangeRed,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
            ToolTip = "Remove item"
        };

    private bool CanRemoveItems()
        => _member.Property?.GetValue(_owner) is IList;

    private void RemoveItem(object? item)
    {
        if (item is null)
        {
            return;
        }

        if (_member.Property?.GetValue(_owner) is IList list && list.Contains(item))
        {
            list.Remove(item);
        }
    }
}
