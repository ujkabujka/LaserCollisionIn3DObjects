using BaseFramework.Core.Services;
using BaseFramework.WpfHost.Models;

namespace BaseFramework.WpfHost.Views;

public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    public void Initialize(Test_Class_3 model, IObjectMetadataProvider provider)
    {
        DataContext = model;
        Inspector.Bind(model, provider);
    }
}
