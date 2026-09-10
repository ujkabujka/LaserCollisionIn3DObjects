using LaserCollisionIn3DObjects.Wpf.Services;
using System.Diagnostics;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        Trace.WriteLine("[Startup] MainWindow constructor entered.");
        InitializeComponent();
        Trace.WriteLine("[Startup] MainWindow.InitializeComponent completed.");

        var renderSyncService = new SceneRenderSyncService(CollisionWorkspaceView.SceneViewport);
        var projectionRenderSyncService = new ProjectionRenderSyncService(ProjectionWorkspaceView.ViewportControl);
        Trace.WriteLine("[Startup] Render services constructed.");
        _viewModel = new MainWindowViewModel(renderSyncService, projectionRenderSyncService);
        DataContext = _viewModel;
        Trace.WriteLine("[Startup] DataContext assigned.");
        Loaded += OnMainWindowLoaded;
        Closing += OnMainWindowClosing;
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        Trace.WriteLine("[Startup] MainWindow Loaded.");
        _viewModel.InitializeViewport();
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Trace.WriteLine("[Shutdown] MainWindow closing.");
        (Application.Current as App)?.Lifetime.RequestShutdown();
        _viewModel.RequestShutdown();
        Loaded -= OnMainWindowLoaded;
        Closing -= OnMainWindowClosing;
    }
}
