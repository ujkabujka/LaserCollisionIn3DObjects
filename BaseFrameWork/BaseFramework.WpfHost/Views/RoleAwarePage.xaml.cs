using BaseFramework.Core.Access;
using BaseFramework.Core.Services;
using BaseFramework.Wpf.Controls;
using BaseFramework.WpfHost.Models;

namespace BaseFramework.WpfHost.Views;

public partial class RoleAwarePage : UserControl
{
    private readonly RoleAwareExampleModel _model = new();
    private IObjectMetadataProvider? _provider;

    public RoleAwarePage()
    {
        InitializeComponent();
    }

    public void Initialize(IObjectMetadataProvider provider)
    {
        _provider = provider;
        SessionSelector.ItemsSource = new[]
        {
            new RoleSimulation("System Engineer", "Can generate documents but cannot edit restricted fields.", InspectableAccessContext.Create("engineer", ["SystemEngineer"], ["documents.generate"])),
            new RoleSimulation("Designer", "Can see designer-only notes but cannot invoke generation.", InspectableAccessContext.Create("designer", ["Designer"], ["templates.manage"])),
            new RoleSimulation("Admin", "Full access including restricted field editing.", InspectableAccessContext.Create("admin", ["Admin"], ["documents.generate", "fields.edit.restricted", "templates.manage"]))
        };

        SessionSelector.SelectedIndex = 0;
    }

    private void OnSessionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_provider is null || SessionSelector.SelectedItem is not RoleSimulation simulation)
        {
            return;
        }

        SessionDescriptionText.Text = simulation.Description;
        Inspector.Bind(
            _model,
            _provider,
            new DefaultMemberAccessEvaluator(),
            simulation.AccessContext,
            new DefaultInspectorEditorRegistry());
    }

    private sealed record RoleSimulation(string Label, string Description, InspectableAccessContext AccessContext);
}
