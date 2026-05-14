using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace LaserCollisionIn3DObjects.Wpf.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached(
        "AutoScrollToEnd",
        typeof(bool),
        typeof(AutoScrollBehavior),
        new PropertyMetadata(false, OnAutoScrollToEndChanged));

    private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
        "Subscription",
        typeof(NotifyCollectionChangedEventHandler),
        typeof(AutoScrollBehavior),
        new PropertyMetadata(null));

    public static bool GetAutoScrollToEnd(DependencyObject obj) => (bool)obj.GetValue(AutoScrollToEndProperty);

    public static void SetAutoScrollToEnd(DependencyObject obj, bool value) => obj.SetValue(AutoScrollToEndProperty, value);

    private static void OnAutoScrollToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            listBox.Loaded += OnListBoxLoaded;
            listBox.DataContextChanged += OnDataContextChanged;
            AttachCollectionChanged(listBox);
        }
        else
        {
            listBox.Loaded -= OnListBoxLoaded;
            listBox.DataContextChanged -= OnDataContextChanged;
            DetachCollectionChanged(listBox);
        }
    }

    private static void OnListBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            ScrollToLatest(listBox);
        }
    }

    private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ListBox listBox)
        {
            DetachCollectionChanged(listBox);
            AttachCollectionChanged(listBox);
            ScrollToLatest(listBox);
        }
    }

    private static void AttachCollectionChanged(ListBox listBox)
    {
        if (listBox.ItemsSource is not INotifyCollectionChanged notifyCollection)
        {
            return;
        }

        NotifyCollectionChangedEventHandler handler = (_, _) => ScrollToLatest(listBox);
        notifyCollection.CollectionChanged += handler;
        listBox.SetValue(SubscriptionProperty, handler);
    }

    private static void DetachCollectionChanged(ListBox listBox)
    {
        if (listBox.ItemsSource is INotifyCollectionChanged notifyCollection &&
            listBox.GetValue(SubscriptionProperty) is NotifyCollectionChangedEventHandler existingHandler)
        {
            notifyCollection.CollectionChanged -= existingHandler;
        }

        listBox.ClearValue(SubscriptionProperty);
    }

    private static void ScrollToLatest(ListBox listBox)
    {
        if (listBox.Items.Count == 0)
        {
            return;
        }

        if (listBox.Items[listBox.Items.Count - 1] is { } item)
        {
            listBox.ScrollIntoView(item);
        }
    }
}
