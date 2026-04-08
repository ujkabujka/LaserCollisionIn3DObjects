using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Services;
using BaseFramework.Wpf.Controls;

namespace BaseFramework.WpfHost.Views;

public partial class InspectorExamplePage : UserControl
{
    public InspectorExamplePage()
    {
        InitializeComponent();
    }

    public void Initialize(
        string title,
        string description,
        ObservableObject model,
        IObjectMetadataProvider provider,
        InspectableAccessContext? accessContext = null)
    {
        TitleText.Text = title;
        DescriptionText.Text = description;
        Inspector.Bind(
            model,
            provider,
            new DefaultMemberAccessEvaluator(),
            accessContext ?? InspectableAccessContext.Empty,
            new DefaultInspectorEditorRegistry());
    }
}
