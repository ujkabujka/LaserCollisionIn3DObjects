using System.Windows;
using System.Windows.Controls;

namespace LaserCollisionIn3DObjects.Wpf.Controls;

public partial class BusyOverlay : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(BusyOverlay), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(BusyOverlay), new PropertyMetadata(0d));
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(
        nameof(IsIndeterminate), typeof(bool), typeof(BusyOverlay), new PropertyMetadata(true));

    public BusyOverlay() => InitializeComponent();

    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
}
