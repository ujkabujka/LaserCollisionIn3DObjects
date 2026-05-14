using HelixToolkit.Wpf;
using System.Windows.Controls;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class CollisionWorkspaceView : UserControl
{
    public CollisionWorkspaceView()
    {
        InitializeComponent();
    }

    public HelixViewport3D SceneViewport => Viewport;
}
