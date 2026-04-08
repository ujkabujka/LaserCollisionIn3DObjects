namespace BaseFramework.Wpf.Controls.Navigation;

public sealed class NavigationPage
{
    public NavigationPage(string title, UIElement content, string? icon = null)
    {
        Title = title;
        Content = content;
        Icon = icon ?? "•";
    }

    public string Title { get; }

    public string Icon { get; }

    public UIElement Content { get; }
}

