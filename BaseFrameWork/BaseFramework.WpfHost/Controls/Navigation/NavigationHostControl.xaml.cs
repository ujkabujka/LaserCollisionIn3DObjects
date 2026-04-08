using System.Collections.ObjectModel;

namespace BaseFramework.WpfHost.Controls.Navigation;

public partial class NavigationHostControl : UserControl
{
    public static readonly DependencyProperty SelectedPageProperty = DependencyProperty.Register(
        nameof(SelectedPage),
        typeof(NavigationPage),
        typeof(NavigationHostControl),
        new PropertyMetadata(null, OnSelectedPageChanged));

    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(
        nameof(IsCollapsed),
        typeof(bool),
        typeof(NavigationHostControl),
        new PropertyMetadata(false, OnIsCollapsedChanged));

    public NavigationHostControl()
    {
        InitializeComponent();
        Pages = new ObservableCollection<NavigationPage>();
        Pages.CollectionChanged += (_, _) => EnsureSelection();
        DataContext = this;
    }

    public ObservableCollection<NavigationPage> Pages { get; }

    public NavigationPage? SelectedPage
    {
        get => (NavigationPage?)GetValue(SelectedPageProperty);
        set => SetValue(SelectedPageProperty, value);
    }

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public void AddPage(NavigationPage page)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        Pages.Add(page);
        EnsureSelection();
    }

    private static void OnSelectedPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NavigationHostControl)d;
        control.ContentHost.Content = e.NewValue is NavigationPage page ? page.Content : null;
    }

    private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NavigationHostControl)d;
        control.ApplyCollapsedState();
    }

    private void OnToggleCollapsed(object sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }

    private void ApplyCollapsedState()
    {
        NavColumn.Width = IsCollapsed ? new GridLength(72) : new GridLength(220);
        NavigationTitle.Visibility = IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
    }

    private void EnsureSelection()
    {
        if (SelectedPage is null && Pages.Count > 0)
        {
            SelectedPage = Pages[0];
            return;
        }

        if (SelectedPage is not null && !Pages.Contains(SelectedPage))
        {
            SelectedPage = Pages.FirstOrDefault();
        }
    }
}
