using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using BaseFramework.Core.Scheduling;

namespace BaseFramework.Wpf.Controls;

public sealed class CalendarEventDetailsControl : UserControl
{
    private const string TitlePropertyName = "Title";
    private const string CompanyPropertyName = "Company";
    private const string CategoryPropertyName = "Category";
    private const string DescriptionPropertyName = "Description";
    private const string StartDatePropertyName = "Start";
    private const string EndDatePropertyName = "End";

    public static readonly DependencyProperty EventProperty = DependencyProperty.Register(
        nameof(Event),
        typeof(ICalendarEvent),
        typeof(CalendarEventDetailsControl),
        new PropertyMetadata(null, OnEventChanged));

    private readonly TextBox _titleBox;
    private readonly TextBox _companyBox;
    private readonly TextBox _categoryBox;
    private readonly TextBox _descriptionBox;
    private readonly DatePicker _startDatePicker;
    private readonly TextBox _startTimeBox;
    private readonly DatePicker _endDatePicker;
    private readonly TextBox _endTimeBox;
    private readonly TextBlock _emptyState;

    private ICalendarEvent? _current;
    private INotifyPropertyChanged? _notifier;
    private bool _isUpdating;

    public CalendarEventDetailsControl()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new TextBlock
        {
            Text = "Event Details",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var contentGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rowIndex = 0;
        void AddLabel(string text)
        {
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, rowIndex);
            Grid.SetColumn(label, 0);
            contentGrid.Children.Add(label);
        }

        FrameworkElement AddControl(FrameworkElement element)
        {
            Grid.SetRow(element, rowIndex);
            Grid.SetColumn(element, 1);
            contentGrid.Children.Add(element);
            rowIndex++;
            return element;
        }

        AddLabel("Title");
        _titleBox = (TextBox)AddControl(new TextBox());

        AddLabel("Printer");
        _companyBox = (TextBox)AddControl(new TextBox());

        AddLabel("Category");
        _categoryBox = (TextBox)AddControl(new TextBox());

        AddLabel("Description");
        _descriptionBox = (TextBox)AddControl(new TextBox { AcceptsReturn = true, MinHeight = 140, Height = 140, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        AddLabel("Start");
        var startStack = new StackPanel { Orientation = Orientation.Horizontal };
        _startDatePicker = new DatePicker { Width = 120, Margin = new Thickness(0, 0, 4, 0) };
        _startTimeBox = new TextBox { Width = 60 };
        _startTimeBox.LostFocus += (_, _) => CommitTime(StartDatePropertyName, _startDatePicker, _startTimeBox);
        FocusHelpers.AttachEnterToCommitAndMoveFocus(_startTimeBox, () => CommitTime(StartDatePropertyName, _startDatePicker, _startTimeBox));
        _startDatePicker.SelectedDateChanged += (_, _) => CommitTime(StartDatePropertyName, _startDatePicker, _startTimeBox);
        startStack.Children.Add(_startDatePicker);
        startStack.Children.Add(_startTimeBox);
        AddControl(startStack);

        AddLabel("End");
        var endStack = new StackPanel { Orientation = Orientation.Horizontal };
        _endDatePicker = new DatePicker { Width = 120, Margin = new Thickness(0, 0, 4, 0) };
        _endTimeBox = new TextBox { Width = 60 };
        _endTimeBox.LostFocus += (_, _) => CommitTime(EndDatePropertyName, _endDatePicker, _endTimeBox);
        FocusHelpers.AttachEnterToCommitAndMoveFocus(_endTimeBox, () => CommitTime(EndDatePropertyName, _endDatePicker, _endTimeBox));
        _endDatePicker.SelectedDateChanged += (_, _) => CommitTime(EndDatePropertyName, _endDatePicker, _endTimeBox);
        endStack.Children.Add(_endDatePicker);
        endStack.Children.Add(_endTimeBox);
        AddControl(endStack);

        Grid.SetRow(contentGrid, 1);
        root.Children.Add(contentGrid);

        _emptyState = new TextBlock
        {
            Text = "Select an event to view details.",
            Opacity = 0.6,
            FontStyle = FontStyles.Italic,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var container = new Grid();
        container.Children.Add(root);
        container.Children.Add(_emptyState);

        Content = container;

        BindField(_titleBox, TitlePropertyName, BindingMode.OneWay);
        BindField(_companyBox, CompanyPropertyName, BindingMode.OneWay);
        BindField(_categoryBox, CategoryPropertyName, BindingMode.OneWay);
        BindField(_descriptionBox, DescriptionPropertyName, BindingMode.OneWay);
    }

    public ICalendarEvent? Event
    {
        get => (ICalendarEvent?)GetValue(EventProperty);
        set => SetValue(EventProperty, value);
    }

    private static void OnEventChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CalendarEventDetailsControl)d;
        control.BindToEvent(e.NewValue as ICalendarEvent);
    }

    private void BindToEvent(ICalendarEvent? newEvent)
    {
        if (ReferenceEquals(_current, newEvent))
        {
            return;
        }

        if (_notifier is not null)
        {
            _notifier.PropertyChanged -= HandleEventPropertyChanged;
            _notifier = null;
        }

        _current = newEvent;
        DataContext = newEvent;
        if (newEvent is INotifyPropertyChanged npc)
        {
            _notifier = npc;
            _notifier.PropertyChanged += HandleEventPropertyChanged;
        }

        UpdateState();
    }

    private void UpdateState()
    {
        var hasEvent = _current is not null;
        _emptyState.Visibility = hasEvent ? Visibility.Collapsed : Visibility.Visible;

        if (!hasEvent)
        {
            _isUpdating = true;
            _startTimeBox.Text = string.Empty;
            _endTimeBox.Text = string.Empty;
            _startDatePicker.SelectedDate = null;
            _endDatePicker.SelectedDate = null;
            _isUpdating = false;
            return;
        }

        _isUpdating = true;
        var startValue = GetDateTime(StartDatePropertyName);
        var endValue = GetDateTime(EndDatePropertyName);
        _startTimeBox.Text = startValue?.ToString("HH:mm") ?? "00:00";
        _endTimeBox.Text = endValue?.ToString("HH:mm") ?? "00:00";
        _startDatePicker.SelectedDate = startValue?.Date;
        _endDatePicker.SelectedDate = endValue?.Date;
        _isUpdating = false;

        UpdateEditability();
    }

    private void BindField(TextBox textBox, string propertyName, BindingMode mode)
    {
        var binding = new Binding(propertyName)
        {
            Mode = mode,
            UpdateSourceTrigger = mode == BindingMode.TwoWay ? UpdateSourceTrigger.LostFocus : UpdateSourceTrigger.PropertyChanged
        };
        textBox.SetBinding(TextBox.TextProperty, binding);
    }

    private void CommitTime(string propertyName, DatePicker picker, TextBox timeBox)
    {
        if (_isUpdating || _current is null)
        {
            return;
        }

        var property = FindWritableProperty(propertyName);
        if (property is null)
        {
            return;
        }

        var baseValue = GetDateTime(propertyName) ?? DateTime.Today;
        var date = picker.SelectedDate ?? baseValue.Date;
        if (!TimeSpan.TryParse(timeBox.Text, CultureInfo.InvariantCulture, out var time))
        {
            time = baseValue.TimeOfDay;
            _isUpdating = true;
            timeBox.Text = time.ToString(@"hh\:mm");
            _isUpdating = false;
        }

        property.SetValue(_current, date.Date + time);
    }

    private DateTime? GetDateTime(string propertyName)
    {
        if (_current is null)
        {
            return null;
        }

        var property = FindProperty(propertyName);
        if (property is null)
        {
            return null;
        }

        return ConvertToNullableDateTime(property.GetValue(_current));
    }

    private static DateTime? ConvertToNullableDateTime(object? value)
    {
        if (value is DateTime dt)
        {
            return dt;
        }

        return null;
    }

    private void HandleEventPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateDatePresentation(e.PropertyName));
    }

    private void UpdateDatePresentation(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return;
        }

        var comparer = StringComparison.OrdinalIgnoreCase;
        if (string.Equals(propertyName, StartDatePropertyName, comparer))
        {
            _isUpdating = true;
            var start = GetDateTime(StartDatePropertyName);
            _startTimeBox.Text = start?.ToString("HH:mm") ?? "00:00";
            _startDatePicker.SelectedDate = start?.Date;
            _isUpdating = false;
        }

        if (string.Equals(propertyName, EndDatePropertyName, comparer))
        {
            _isUpdating = true;
            var end = GetDateTime(EndDatePropertyName);
            _endTimeBox.Text = end?.ToString("HH:mm") ?? "00:00";
            _endDatePicker.SelectedDate = end?.Date;
            _isUpdating = false;
        }
    }

    private void UpdateEditability()
    {
        SetEditableState(_titleBox, TitlePropertyName);
        SetEditableState(_companyBox, CompanyPropertyName);
        SetEditableState(_categoryBox, CategoryPropertyName);
        SetEditableState(_descriptionBox, DescriptionPropertyName);

        var canEditStart = CanWrite(StartDatePropertyName);
        _startDatePicker.IsEnabled = canEditStart;
        _startTimeBox.IsReadOnly = !canEditStart;

        var canEditEnd = CanWrite(EndDatePropertyName);
        _endDatePicker.IsEnabled = canEditEnd;
        _endTimeBox.IsReadOnly = !canEditEnd;
    }

    private void SetEditableState(TextBox textBox, string propertyName)
        => textBox.IsReadOnly = !CanWrite(propertyName);

    private bool CanWrite(string propertyName) => FindWritableProperty(propertyName) is not null;

    private PropertyInfo? FindProperty(string propertyName)
    {
        if (_current is null)
        {
            return null;
        }

        return _current.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
    }

    private PropertyInfo? FindWritableProperty(string propertyName)
    {
        var property = FindProperty(propertyName);
        if (property is null || !property.CanWrite)
        {
            return null;
        }

        return property;
    }
}

