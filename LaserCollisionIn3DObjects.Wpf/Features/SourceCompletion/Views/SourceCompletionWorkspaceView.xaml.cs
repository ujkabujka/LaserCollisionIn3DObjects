using System.Windows.Controls;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Features.SourceCompletion.Views;

public partial class SourceCompletionWorkspaceView : UserControl
{
    public SourceCompletionWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel mainWindowViewModel)
        {
            mainWindowViewModel.SourceCompletionWorkspace.AttachViewport(SourceCompletionViewport);
        }
    }
}
