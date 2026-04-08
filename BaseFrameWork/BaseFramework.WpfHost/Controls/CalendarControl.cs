using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using BaseFramework.Core.Scheduling;
using BaseFramework.Core.Attributes;

namespace BaseFramework.WpfHost.Controls;

public sealed class CalendarControl : UserControl
{
    private const string AllPrintersFilter = "All Printers";
    private const double MinutesPerDay = 24d * 60d;

    public static readonly DependencyProperty EventsSourceProperty = DependencyProperty.Register(
        nameof(EventsSource),
        typeof(ObservableCollection<ICalendarEvent>),
        typeof(CalendarControl),
        new PropertyMetadata(null, (d, _) => ((CalendarControl)d).AttachEvents()));

    public static readonly DependencyProperty SelectedEventProperty = DependencyProperty.Register(
        nameof(SelectedEvent),
        typeof(ICalendarEvent),
        typeof(CalendarControl),
        new PropertyMetadata(null, OnSelectedEventChanged));

    public static readonly DependencyProperty IsDetailsPanelEnabledProperty = DependencyProperty.Register(
        nameof(IsDetailsPanelEnabled),
        typeof(bool),
        typeof(CalendarControl),
        new PropertyMetadata(true, (d, _) => ((CalendarControl)d).UpdateDetailsPanelState()));

    public static readonly DependencyProperty PrinterItemsSourceProperty = DependencyProperty.Register(
        nameof(PrinterItemsSource),
        typeof(IEnumerable),
        typeof(CalendarControl),
        new PropertyMetadata(null, (d, _) => ((CalendarControl)d).AttachPrinterItemsSource()));

    private TextBlock _monthLabel = null!;
    private ComboBox _companyFilter = null!;
    private Grid _calendarGrid = null!;
    private readonly CalendarEventDetailsControl _detailsControl;
    private readonly ColumnDefinition _detailsColumn;
    private readonly ColumnDefinition _splitterColumn;
    private readonly Border _detailsBorder;
    private readonly GridSplitter _detailsSplitter;
    private readonly Dictionary<Guid, SolidColorBrush> _eventBrushes = new();
    private readonly Dictionary<ICalendarEvent, PropertyChangedEventHandler> _eventSubscriptions = new();
    private readonly Dictionary<ICalendarEvent, INotifyCollectionChanged> _eventCollectionSubscriptions = new();
    private readonly Dictionary<ICalendarEvent, List<Border>> _eventBarLookup = new();
    private readonly Brush _accentBrush;
    private INotifyCollectionChanged? _collectionChanges;
    private INotifyCollectionChanged? _printerItemsChanges;
    private DateTime _visibleMonth;

    public CalendarControl()
    {
        _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _accentBrush = (Brush)(TryFindResource("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(14, 99, 156)));

        var calendarBorder = BuildCalendarHost();

        _detailsControl = new CalendarEventDetailsControl();
        _detailsBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(16, 0, 0, 0),
            Child = _detailsControl
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
        _splitterColumn = new ColumnDefinition { Width = GridLength.Auto };
        root.ColumnDefinitions.Add(_splitterColumn);
        _detailsColumn = new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) };
        root.ColumnDefinitions.Add(_detailsColumn);

        Grid.SetColumn(calendarBorder, 0);
        root.Children.Add(calendarBorder);

        _detailsSplitter = new GridSplitter
        {
            Width = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            ShowsPreview = true
        };
        Grid.SetColumn(_detailsSplitter, 1);
        root.Children.Add(_detailsSplitter);

        Grid.SetColumn(_detailsBorder, 2);
        root.Children.Add(_detailsBorder);

        Content = root;

        BuildGridStructure();
        RefreshMonthLabel();
        RefreshCalendar();
        UpdateDetailsPanelState();

        Unloaded += (_, _) =>
        {
            DetachEvents();
            DetachPrinterItemsSource();
        };
    }

    public ObservableCollection<ICalendarEvent>? EventsSource
    {
        get => (ObservableCollection<ICalendarEvent>?)GetValue(EventsSourceProperty);
        set => SetValue(EventsSourceProperty, value);
    }

    public ICalendarEvent? SelectedEvent
    {
        get => (ICalendarEvent?)GetValue(SelectedEventProperty);
        set => SetValue(SelectedEventProperty, value);
    }

    public bool IsDetailsPanelEnabled
    {
        get => (bool)GetValue(IsDetailsPanelEnabledProperty);
        set => SetValue(IsDetailsPanelEnabledProperty, value);
    }

    public IEnumerable? PrinterItemsSource
    {
        get => (IEnumerable?)GetValue(PrinterItemsSourceProperty);
        set => SetValue(PrinterItemsSourceProperty, value);
    }

    [InspectableMember("calendar.refresh", "Refresh", Order = 0)]
    public void Refresh()
    {
        RefreshFilterOptions();
        RefreshCalendar();
        EnsureSelectedEventIsValid();
        UpdateSelectionVisuals();
        InvalidateVisual();
        UpdateLayout();
    }

    private Border BuildCalendarHost()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var prevButton = new Button { Content = "<", Width = 36, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
        prevButton.Click += (_, _) => ChangeMonth(-1);
        Grid.SetColumn(prevButton, 0);
        header.Children.Add(prevButton);

        _monthLabel = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        };
        Grid.SetColumn(_monthLabel, 1);
        header.Children.Add(_monthLabel);

        var nextButton = new Button { Content = ">", Width = 36, Height = 30, Margin = new Thickness(8, 0, 0, 0) };
        nextButton.Click += (_, _) => ChangeMonth(1);
        Grid.SetColumn(nextButton, 2);
        header.Children.Add(nextButton);

        var filterPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var filterLabel = new TextBlock
        {
            Text = "Printer:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = TryFindResource("TextBrush") as Brush ?? Brushes.White
        };
        _companyFilter = new ComboBox
        {
            Width = 200,
            Background = Brushes.Black,
            Foreground = Brushes.White,
            BorderBrush = _accentBrush,
            Padding = new Thickness(4, 2, 4, 2)
        };
        _companyFilter.SelectionChanged += (_, _) => RefreshCalendar();

        var refreshButton = new Button
        {
            Content = "Refresh",
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 90
        };
        refreshButton.Click += (_, _) => Refresh();

        filterPanel.Children.Add(filterLabel);
        filterPanel.Children.Add(_companyFilter);
        filterPanel.Children.Add(refreshButton);

        _calendarGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };

        root.Children.Add(header);
        Grid.SetRow(filterPanel, 1);
        root.Children.Add(filterPanel);
        Grid.SetRow(_calendarGrid, 2);
        root.Children.Add(_calendarGrid);

        return new Border
        {
            Background = TryFindResource("SurfaceBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(37, 37, 38)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = root
        };
    }

    private static void OnSelectedEventChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CalendarControl)d;
        control._detailsControl.Event = e.NewValue as ICalendarEvent;
        control.UpdateSelectionVisuals();
    }

    private void ChangeMonth(int offset)
    {
        _visibleMonth = _visibleMonth.AddMonths(offset);
        RefreshMonthLabel();
        RefreshCalendar();
    }

    private void RefreshMonthLabel()
    {
        _monthLabel.Text = _visibleMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }

    private void BuildGridStructure()
    {
        _calendarGrid.RowDefinitions.Clear();
        _calendarGrid.ColumnDefinitions.Clear();
        _calendarGrid.Children.Clear();

        for (var i = 0; i < 7; i++)
        {
            _calendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        _calendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < 6; i++)
        {
            _calendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
    }

    private void RefreshCalendar()
    {
        _eventBarLookup.Clear();
        AddWeekdayHeaders();
        AddDayCells();
    }

    private void AddWeekdayHeaders()
    {
        _calendarGrid.Children.Clear();

        var days = new[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday,
            DayOfWeek.Sunday
        };

        foreach (var (day, column) in days.Select((day, index) => (day, index)))
        {
            var label = new TextBlock
            {
                Text = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day).ToUpperInvariant(),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };

            Grid.SetRow(label, 0);
            Grid.SetColumn(label, column);
            _calendarGrid.Children.Add(label);
        }
    }

    private void AddDayCells()
    {
        var firstDay = new DateTime(_visibleMonth.Year, _visibleMonth.Month, 1);
        var start = firstDay;
        while (start.DayOfWeek != DayOfWeek.Monday)
        {
            start = start.AddDays(-1);
        }

        for (var week = 0; week < 6; week++)
        {
            for (var day = 0; day < 7; day++)
            {
                var current = start.AddDays(week * 7 + day);
                var cell = BuildDayCell(current, current.Month == _visibleMonth.Month);
                Grid.SetRow(cell, week + 1);
                Grid.SetColumn(cell, day);
                _calendarGrid.Children.Add(cell);
            }
        }
    }

    private FrameworkElement BuildDayCell(DateTime date, bool isCurrentMonth)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(0.5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
            Margin = new Thickness(1),
            Background = isCurrentMonth ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

        var header = new TextBlock
        {
            Text = date.Day.ToString(CultureInfo.InvariantCulture),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = isCurrentMonth ? Brushes.White : new SolidColorBrush(Color.FromRgb(170, 170, 170))
        };
        stack.Children.Add(header);

        foreach (var evt in GetEventsForDay(date))
        {
            stack.Children.Add(CreateEventBar(border, date, evt));
        }

        if (!stack.Children.OfType<FrameworkElement>().Skip(1).Any())
        {
            stack.Children.Add(new TextBlock { Text = "-", Opacity = 0.2, HorizontalAlignment = HorizontalAlignment.Center });
        }

        border.Child = stack;
        return border;
    }

    private FrameworkElement CreateEventBar(Border dayContainer, DateTime day, ICalendarEvent calendarEvent)
    {
        var dayStart = day.Date;
        var startMinutes = Math.Clamp((calendarEvent.Start - dayStart).TotalMinutes, 0, MinutesPerDay);
        var endMinutes = Math.Clamp((calendarEvent.End - dayStart).TotalMinutes, startMinutes, MinutesPerDay);
        var durationMinutes = Math.Max(endMinutes - startMinutes, 15);
        var trailingMinutes = Math.Max(MinutesPerDay - startMinutes - durationMinutes, 0);

        var grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.SetBinding(WidthProperty, new Binding("ActualWidth") { Source = dayContainer });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(startMinutes, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(durationMinutes, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(trailingMinutes, GridUnitType.Star) });

        var bar = new Border
        {
            Background = GetBrushForEvent(calendarEvent),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent
        };

        var label = new TextBlock
        {
            Text = calendarEvent.Title,
            FontSize = 12,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        bar.Child = label;

        bar.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            SelectedEvent = calendarEvent;
        };

        RegisterEventBar(calendarEvent, bar);
        BuildTooltip(bar, calendarEvent);

        Grid.SetColumn(bar, 1);
        grid.Children.Add(bar);
        return grid;
    }

    private void RegisterEventBar(ICalendarEvent calendarEvent, Border bar)
    {
        if (!_eventBarLookup.TryGetValue(calendarEvent, out var list))
        {
            list = new List<Border>();
            _eventBarLookup[calendarEvent] = list;
        }

        list.Add(bar);
    }

    private void BuildTooltip(Border bar, ICalendarEvent calendarEvent)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = calendarEvent.Title, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        stack.Children.Add(new TextBlock { Text = calendarEvent.Company, Foreground = Brushes.White });
        var timeText = $"{calendarEvent.Start:g} - {calendarEvent.End:g}";
        stack.Children.Add(new TextBlock { Text = timeText, Foreground = Brushes.White });
        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            stack.Children.Add(new TextBlock { Text = calendarEvent.Description!, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White });
        }

        var tooltip = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
            BorderBrush = _accentBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = stack
        };

        ToolTipService.SetToolTip(bar, tooltip);
    }

    private IEnumerable<ICalendarEvent> GetEventsForDay(DateTime day)
    {
        var events = EventsSource?.AsEnumerable() ?? Enumerable.Empty<ICalendarEvent>();
        var selectedPrinter = _companyFilter.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(selectedPrinter) && !string.Equals(selectedPrinter, AllPrintersFilter, StringComparison.OrdinalIgnoreCase))
        {
            events = events.Where(evt => string.Equals(evt.Company, selectedPrinter, StringComparison.OrdinalIgnoreCase));
        }

        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        return events
            .Where(evt => evt.Start < dayEnd && evt.End > dayStart)
            .OrderBy(evt => evt.Start)
            .ThenBy(evt => evt.End);
    }

    private SolidColorBrush GetBrushForEvent(ICalendarEvent calendarEvent)
    {
        if (_eventBrushes.TryGetValue(calendarEvent.EventId, out var brush))
        {
            return brush;
        }

        var seed = calendarEvent.EventId.GetHashCode();
        var random = new Random(seed);
        var hue = random.NextDouble();
        var saturation = 0.55 + random.NextDouble() * 0.35;
        var lightness = 0.62 + random.NextDouble() * 0.18;

        var created = new SolidColorBrush(FromHsl(hue, saturation, lightness));
        created.Freeze();
        _eventBrushes[calendarEvent.EventId] = created;
        return created;
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s <= 0)
        {
            var gray = (byte)Math.Clamp((int)Math.Round(l * 255), 0, 255);
            return Color.FromRgb(gray, gray, gray);
        }

        static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        var r = HueToRgb(p, q, h + 1.0 / 3.0);
        var g = HueToRgb(p, q, h);
        var b = HueToRgb(p, q, h - 1.0 / 3.0);
        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(r * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b * 255), 0, 255));
    }

    private void AttachEvents()
    {
        DetachEvents();

        var source = EventsSource;
        if (source is null)
        {
            Refresh();
            return;
        }

        if (source is INotifyCollectionChanged notify)
        {
            _collectionChanges = notify;
            _collectionChanges.CollectionChanged += HandleEventsCollectionChanged;
        }

        foreach (var evt in source)
        {
            RegisterEvent(evt);
        }

        Refresh();
        AttachPrinterItemsSource();
    }

    private void RegisterEvent(ICalendarEvent evt)
    {
        if (!_eventSubscriptions.ContainsKey(evt))
        {
            PropertyChangedEventHandler propertyChangedHandler = HandleEventPropertyChanged;
            evt.PropertyChanged += propertyChangedHandler;
            _eventSubscriptions[evt] = propertyChangedHandler;
        }

        if (evt is INotifyCollectionChanged collectionChanged && !_eventCollectionSubscriptions.ContainsKey(evt))
        {
            collectionChanged.CollectionChanged += HandleEventNestedCollectionChanged;
            _eventCollectionSubscriptions[evt] = collectionChanged;
        }
    }

    private void HandleEventsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var added in e.NewItems.OfType<ICalendarEvent>())
            {
                RegisterEvent(added);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (var removed in e.OldItems.OfType<ICalendarEvent>())
            {
                if (_eventSubscriptions.Remove(removed, out var propertyChangedHandler))
                {
                    removed.PropertyChanged -= propertyChangedHandler;
                }

                if (_eventCollectionSubscriptions.Remove(removed, out var collectionChanged))
                {
                    collectionChanged.CollectionChanged -= HandleEventNestedCollectionChanged;
                }
            }
        }

        Dispatcher.Invoke(() =>
        {
            Refresh();
        });
    }

    private void HandleEventPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Refresh();
        });
    }

    private void HandleEventNestedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Refresh();
        });
    }

    private void AttachPrinterItemsSource()
    {
        DetachPrinterItemsSource();

        if (PrinterItemsSource is INotifyCollectionChanged notify)
        {
            _printerItemsChanges = notify;
            _printerItemsChanges.CollectionChanged += HandlePrinterItemsCollectionChanged;
        }

        RefreshFilterOptions();
    }

    private void DetachPrinterItemsSource()
    {
        if (_printerItemsChanges is null)
        {
            return;
        }

        _printerItemsChanges.CollectionChanged -= HandlePrinterItemsCollectionChanged;
        _printerItemsChanges = null;
    }

    private void HandlePrinterItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Refresh();
        });
    }

    private List<string> BuildPrinterFilterOptions()
    {
        var fromInventory = (PrinterItemsSource ?? Enumerable.Empty<object>())
            .OfType<object?>()
            .Select(ExtractPrinterName)
            .OfType<string>()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fromInventory.Count > 0)
        {
            return fromInventory;
        }

        return (EventsSource?.AsEnumerable() ?? Enumerable.Empty<ICalendarEvent>())
            .Select(evt => evt.Company)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ExtractPrinterName(object? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item is string text)
        {
            return text;
        }

        var property = item.GetType().GetProperty("Name");
        return property?.GetValue(item)?.ToString();
    }

    private void RefreshFilterOptions()
    {
        var printers = BuildPrinterFilterOptions();
        printers.Insert(0, AllPrintersFilter);

        var previous = _companyFilter.SelectedItem as string;
        _companyFilter.ItemsSource = printers;

        if (!string.IsNullOrEmpty(previous))
        {
            var match = printers.FirstOrDefault(c => string.Equals(c, previous, StringComparison.OrdinalIgnoreCase));
            _companyFilter.SelectedItem = match ?? AllPrintersFilter;
        }
        else
        {
            _companyFilter.SelectedIndex = 0;
        }
    }

    private void DetachEvents()
    {
        if (_collectionChanges is not null)
        {
            _collectionChanges.CollectionChanged -= HandleEventsCollectionChanged;
            _collectionChanges = null;
        }

        foreach (var subscription in _eventSubscriptions.ToList())
        {
            subscription.Key.PropertyChanged -= subscription.Value;
        }

        foreach (var subscription in _eventCollectionSubscriptions.ToList())
        {
            subscription.Value.CollectionChanged -= HandleEventNestedCollectionChanged;
        }

        _eventSubscriptions.Clear();
        _eventCollectionSubscriptions.Clear();
    }

    private void EnsureSelectedEventIsValid()
    {
        if (SelectedEvent is null)
        {
            return;
        }

        var events = EventsSource;
        if (events is null || !events.Contains(SelectedEvent))
        {
            SelectedEvent = null;
        }
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var pair in _eventBarLookup)
        {
            foreach (var bar in pair.Value)
            {
                var isSelected = ReferenceEquals(pair.Key, SelectedEvent);
                bar.BorderBrush = isSelected ? _accentBrush : Brushes.Transparent;
                bar.BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            }
        }
    }

    private void UpdateDetailsPanelState()
    {
        if (IsDetailsPanelEnabled)
        {
            _splitterColumn.Width = GridLength.Auto;
            _detailsColumn.Width = new GridLength(2, GridUnitType.Star);
            _detailsBorder.Visibility = Visibility.Visible;
            _detailsSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            _splitterColumn.Width = new GridLength(0);
            _detailsColumn.Width = new GridLength(0);
            _detailsBorder.Visibility = Visibility.Collapsed;
            _detailsSplitter.Visibility = Visibility.Collapsed;
        }
    }
}
