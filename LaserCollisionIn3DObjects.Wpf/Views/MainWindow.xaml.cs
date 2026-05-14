using LaserCollisionIn3DObjects.Wpf.Services;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var renderSyncService = new SceneRenderSyncService(CollisionWorkspaceView.SceneViewport);
        var projectionRenderSyncService = new ProjectionRenderSyncService(ProjectionWorkspaceView.ViewportControl);
        DataContext = new MainWindowViewModel(renderSyncService, projectionRenderSyncService);
    }
}
