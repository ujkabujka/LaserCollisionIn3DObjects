using System.Windows.Controls;
using LaserCollisionIn3DObjects.Wpf.Features.GraphicMaster.ViewModels;
using LaserCollisionIn3DObjects.Wpf.ViewModels;

namespace LaserCollisionIn3DObjects.Wpf.Views;

public partial class GraphicMasterView : UserControl
{
    public GraphicMasterView()
    {
        InitializeComponent();
    }

    private void OnChartPlotViewSizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { GraphicMasterWorkspace: GraphicMasterViewModel workspace })
        {
            workspace.UpdateExportSize(e.NewSize.Width, e.NewSize.Height);
        }
    }
}
