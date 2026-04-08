using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.WpfHost.Controls;

public sealed class ClassEditorControl : UserControl
{
    private readonly string _displayName;
    private readonly IObjectMetadataProvider _provider;
    private readonly ObservableObject? _owner;
    private readonly InspectableMemberMetadata? _member;
    private readonly Border _contentBorder;
    private readonly TextBlock _headerText;
    private readonly TextBlock _emptyState;
    private readonly ComboBox? _typeSelector;
    private readonly IReadOnlyList<TypeOption> _typeOptions = Array.Empty<TypeOption>();
    private readonly SolidColorBrush _expandedBorderBrush = new(Color.FromRgb(14, 99, 156));
    private ObservableObject? _target;
    private ObjectInspectorControl? _inspector;
    private PropertyChangedEventHandler? _targetNameHandler;

    public ClassEditorControl(string displayName, ObservableObject target, IObjectMetadataProvider provider, ObservableObject? owner = null, InspectableMemberMetadata? member = null)
    {
        _displayName = displayName;
        _target = target;
        _provider = provider;
        _owner = owner;
        _member = member;

        var host = new StackPanel();

        _headerText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerPanel.Children.Add(_headerText);

        if (_owner is not null && _member?.Property is not null)
        {
            _typeOptions = BuildTypeOptions(_member.Property.PropertyType);
            if (_typeOptions.Count > 1)
            {
                _typeSelector = new ComboBox
                {
                    Width = 180,
                    Margin = new Thickness(12, 0, 0, 0),
                    ItemsSource = _typeOptions,
                    DisplayMemberPath = nameof(TypeOption.DisplayName),
                    SelectedValuePath = nameof(TypeOption.Type),
                    IsEnabled = _member is not null && MemberAccess.CanWrite(_member)
                };
                _typeSelector.SelectionChanged += HandleTypeSelectionChanged;
                headerPanel.Children.Add(_typeSelector);
            }
        }

        _contentBorder = new Border
        {
            Margin = new Thickness(12, 4, 0, 4),
            Padding = new Thickness(8, 6, 6, 6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            BorderBrush = Brushes.Transparent,
            Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
            Visibility = Visibility.Collapsed
        };

        _emptyState = new TextBlock
        {
            Text = "No instance assigned.",
            Opacity = 0.6,
            FontStyle = FontStyles.Italic
        };

        var isJobFormRoot = string.Equals(_member?.Key, "jobForm.job", StringComparison.OrdinalIgnoreCase);
        var toggle = new Expander
        {
            Header = headerPanel,
            IsExpanded = isJobFormRoot
        };

        toggle.Expanded += (_, _) =>
        {
            RefreshInspector();
            _contentBorder.Visibility = Visibility.Visible;
            _contentBorder.BorderBrush = _expandedBorderBrush;
        };

        toggle.Collapsed += (_, _) =>
        {
            _contentBorder.Visibility = Visibility.Collapsed;
            _contentBorder.BorderBrush = Brushes.Transparent;
        };

        if (_owner is not null && _member?.Property is not null)
        {
            _owner.PropertyChanged += HandleOwnerPropertyChanged;
            Unloaded += (_, _) => _owner.PropertyChanged -= HandleOwnerPropertyChanged;
        }

        Unloaded += (_, _) => DetachTargetHandlers();

        host.Children.Add(toggle);
        host.Children.Add(_contentBorder);
        Content = host;

        if (isJobFormRoot)
        {
            _contentBorder.Visibility = Visibility.Visible;
            _contentBorder.BorderBrush = _expandedBorderBrush;
            RefreshInspector();
        }

        BindTarget(_target);
    }

    private void HandleOwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_member?.Property is null || string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (!string.Equals(e.PropertyName, _member.Property.Name, StringComparison.Ordinal))
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(UpdateTargetFromOwner);
        }
        else
        {
            UpdateTargetFromOwner();
        }
    }

    private void UpdateTargetFromOwner()
    {
        if (_owner is null || _member?.Property is null)
        {
            return;
        }

        var newValue = _member.Property.GetValue(_owner) as ObservableObject;
        BindTarget(newValue);
    }

    private void HandleTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_typeSelector?.SelectedValue is not Type selectedType || _member?.Property is null || _owner is null || !MemberAccess.CanWrite(_member))
        {
            return;
        }

        if (_target is not null && _target.GetType() == selectedType)
        {
            return;
        }

        if (!typeof(ObservableObject).IsAssignableFrom(selectedType))
        {
            return;
        }

        if (Activator.CreateInstance(selectedType) is not ObservableObject instance)
        {
            return;
        }

        _member.Property.SetValue(_owner, instance);
        BindTarget(instance);
    }

    private void BindTarget(ObservableObject? newTarget)
    {
        if (ReferenceEquals(_target, newTarget))
        {
            UpdateHeaderText();
            UpdateTypeSelection();
            return;
        }

        DetachTargetHandlers();
        _target = newTarget;
        if (_target is not null)
        {
            _targetNameHandler = (_, args) =>
            {
                if (!string.IsNullOrEmpty(args.PropertyName) && !string.Equals(args.PropertyName, "Name", StringComparison.Ordinal))
                {
                    return;
                }

                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(UpdateHeaderText);
                }
                else
                {
                    UpdateHeaderText();
                }
            };
            _target.PropertyChanged += _targetNameHandler;
        }

        RefreshInspector();
        UpdateHeaderText();
        UpdateTypeSelection();
    }

    private void RefreshInspector()
    {
        if (_target is null)
        {
            _contentBorder.Child = _emptyState;
            return;
        }

        _inspector ??= new ObjectInspectorControl();
        _inspector.Bind(_target, _provider);
        _contentBorder.Child = _inspector;
    }

    private void UpdateHeaderText()
    {
        if (_target is not null)
        {
            var nameProperty = _target.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
            if (nameProperty?.GetValue(_target) is string text && !string.IsNullOrWhiteSpace(text))
            {
                _headerText.Text = text;
                return;
            }

            _headerText.Text = _target.GetType().Name;
            return;
        }

        _headerText.Text = _displayName;
    }

    private void UpdateTypeSelection()
    {
        if (_typeSelector is null || _target is null)
        {
            return;
        }

        var currentType = _target.GetType();
        var option = _typeOptions.FirstOrDefault(o => o.Type == currentType);
        _typeSelector.SelectedValue = option?.Type ?? currentType;
    }

    private IReadOnlyList<TypeOption> BuildTypeOptions(Type declaredType)
    {
        var list = new List<TypeOption>();
        var seen = new HashSet<Type>();
        foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes))
        {
            if (type is null || !declaredType.IsAssignableFrom(type) || type.IsAbstract || seen.Contains(type))
            {
                continue;
            }

            if (!typeof(ObservableObject).IsAssignableFrom(type))
            {
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            seen.Add(type);
            list.Add(new TypeOption(type, type.Name));
        }

        return list
            .OrderBy(option => option.Type == declaredType ? 0 : 1)
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private void DetachTargetHandlers()
    {
        if (_target is not null && _targetNameHandler is not null)
        {
            _target.PropertyChanged -= _targetNameHandler;
        }

        _targetNameHandler = null;
    }

    private sealed record TypeOption(Type Type, string DisplayName);
}
