using HelixToolkit.Wpf;
using System.Windows.Controls;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class ProjectionWorkspaceView : UserControl
{
    public ProjectionWorkspaceView()
    {
        InitializeComponent();
    }

    public HelixViewport3D ViewportControl => ProjectionViewport;
}
