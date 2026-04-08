using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using System.Windows;
using BaseFramework.Core;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.WpfHost.Controls;

public sealed class MethodEditorControl : UserControl
{
    public MethodEditorControl(ObservableObject target, InspectableMemberMetadata member, IObjectMetadataProvider provider)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };

        var hasParameters = (member.Parameters?.Count ?? 0) > 0;
        var header = new Grid();
        if (hasParameters)
        {
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        else
        {
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        Expander? toggle = null;
        if (hasParameters)
        {
            toggle = new Expander
            {
                Width = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsExpanded = false,
                Header = string.Empty,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(0)
            };
            Grid.SetColumn(toggle, 0);
            header.Children.Add(toggle);
        }

        var invokeButton = new Button { Content = member.DisplayName, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetColumn(invokeButton, hasParameters ? 1 : 0);
        header.Children.Add(invokeButton);
        panel.Children.Add(header);

        Border? parameterHost = null;
        List<Func<object?>>? editors = null;

        if (hasParameters)
        {
            parameterHost = new Border
            {
                Margin = new Thickness(12, 4, 0, 0),
                Padding = new Thickness(8, 6, 6, 6),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
                BorderBrush = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            panel.Children.Add(parameterHost);

            toggle!.Expanded += (_, _) =>
            {
                if (parameterHost.Child is null)
                {
                    var build = BuildParameterEditors(member.Parameters!, provider);
                    editors = build.Editors;
                    parameterHost.Child = build.Panel;
                }

                parameterHost.Visibility = Visibility.Visible;
                parameterHost.BorderBrush = new SolidColorBrush(Color.FromRgb(14, 99, 156));
            };

            toggle.Collapsed += (_, _) =>
            {
                parameterHost.Visibility = Visibility.Collapsed;
                parameterHost.BorderBrush = Brushes.Transparent;
            };
        }

        invokeButton.Click += (_, _) =>
        {
            if (member.Method is null)
            {
                return;
            }

            try
            {
                if (!hasParameters)
                {
                    member.Method.Invoke(target, Array.Empty<object>());
                    return;
                }

                if (editors is null && member.Parameters is not null)
                {
                    var build = BuildParameterEditors(member.Parameters, provider);
                    editors = build.Editors;
                }

                var values = editors?.Select(get => get()).ToArray() ?? Array.Empty<object?>();
                member.Method.Invoke(target, values);
            }
            catch (Exception ex)
            {
                var root = ex is System.Reflection.TargetInvocationException { InnerException: not null }
                    ? ex.InnerException!
                    : ex;
                MessageBox.Show(root.Message, "İşlem Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        Content = panel;
    }

    private static (StackPanel Panel, List<Func<object?>> Editors) BuildParameterEditors(IReadOnlyList<InspectableMemberMetadata> parameters, IObjectMetadataProvider provider)
    {
        var panel = new StackPanel();
        var editors = new List<Func<object?>>();

        foreach (var parameter in parameters)
        {
            var editor = BuildParameterEditor(parameter, provider);
            editors.Add(editor.Getter);
            panel.Children.Add(editor.Row);
        }

        return (panel, editors);
    }

    private static (FrameworkElement Row, Func<object?> Getter) BuildParameterEditor(InspectableMemberMetadata parameter, IObjectMetadataProvider provider)
    {
        if (parameter.Kind == MemberKind.Class && typeof(ObservableObject).IsAssignableFrom(parameter.ValueType))
        {
            var instance = parameter.DefaultValue as ObservableObject ?? Activator.CreateInstance(parameter.ValueType) as ObservableObject;
            if (instance is not null)
            {
                var classControl = new ClassEditorControl($"{parameter.DisplayName} (param)", instance, provider);
                return (classControl, () => instance);
            }
        }

        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock { Text = parameter.DisplayName, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        if (parameter.ValueType == typeof(bool))
        {
            var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center, IsChecked = parameter.DefaultValue as bool? ?? false };
            Grid.SetColumn(check, 1);
            row.Children.Add(check);
            return (row, () => check.IsChecked ?? false);
        }

        if (parameter.ValueType.IsEnum)
        {
            var options = Enum.GetValues(parameter.ValueType);
            var combo = new ComboBox { ItemsSource = options };
            combo.SelectedItem = parameter.DefaultValue ?? options.GetValue(0);
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            return (row, () => combo.SelectedItem);
        }

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        var defaultText = Convert.ToString(parameter.DefaultValue ?? GetDefault(parameter.ValueType), CultureInfo.InvariantCulture) ?? string.Empty;
        var text = new TextBox { Width = 150, Text = defaultText };
        stack.Children.Add(text);

        if (parameter.Kind is MemberKind.Integer or MemberKind.Double)
        {
            var step = parameter.Kind == MemberKind.Integer ? 1d : 5d;
            var plus = new Button { Content = $"+{step}" };
            var minus = new Button { Content = $"-{step}" };
            plus.Click += (_, _) => text.Text = ApplyStep(text.Text, step, parameter.ValueType);
            minus.Click += (_, _) => text.Text = ApplyStep(text.Text, -step, parameter.ValueType);
            stack.Children.Add(plus);
            stack.Children.Add(minus);
        }

        Grid.SetColumn(stack, 1);
        row.Children.Add(stack);
        return (row, () => ConvertFrom(text.Text, parameter.ValueType));
    }

    private static string ApplyStep(string current, double step, Type type)
    {
        if (type == typeof(int) && int.TryParse(current, out var i))
        {
            return (i + (int)step).ToString(CultureInfo.InvariantCulture);
        }

        if (type == typeof(double) && NumericParsing.TryParseFlexibleDouble(current, out var d))
        {
            return (d + step).ToString(CultureInfo.InvariantCulture);
        }

        return current;
    }

    private static double ParseFlexibleDouble(string text)
    {
        if (NumericParsing.TryParseFlexibleDouble(text, out var value))
        {
            return value;
        }

        throw new FormatException($"Invalid numeric value: {text}");
    }

    private static object? GetDefault(Type type)
    {
        if (!type.IsValueType)
        {
            return string.Empty;
        }

        return Activator.CreateInstance(type);
    }

    private static object? ConvertFrom(string text, Type type)
    {
        if (type == typeof(double))
        {
            return ParseFlexibleDouble(text);
        }

        if (type == typeof(float))
        {
            return (float)ParseFlexibleDouble(text);
        }

        if (type == typeof(decimal))
        {
            return (decimal)ParseFlexibleDouble(text);
        }

        if (type == typeof(int))
        {
            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type == typeof(long))
        {
            return long.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type == typeof(short))
        {
            return short.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type == typeof(bool))
        {
            return bool.Parse(text);
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, text, ignoreCase: true);
        }

        if (type == typeof(string))
        {
            return text;
        }

        return null;
    }
}
