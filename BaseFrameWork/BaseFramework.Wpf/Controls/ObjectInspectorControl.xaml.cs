using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Metadata;
using BaseFramework.Core.Services;

namespace BaseFramework.Wpf.Controls;

public partial class ObjectInspectorControl : UserControl
{
    private ObservableObject? _target;
    private IObjectMetadataProvider? _provider;
    private IMemberAccessEvaluator _accessEvaluator = new DefaultMemberAccessEvaluator();
    private InspectableAccessContext _accessContext = InspectableAccessContext.Empty;
    private IInspectorEditorRegistry _editorRegistry = new DefaultInspectorEditorRegistry();
    private bool _isRebuilding;
    private bool _pendingRebuild;

    public ObjectInspectorControl()
    {
        InitializeComponent();
    }

    public void Bind(ObservableObject target, IObjectMetadataProvider provider)
        => Bind(target, provider, new DefaultMemberAccessEvaluator(), InspectableAccessContext.Empty, new DefaultInspectorEditorRegistry());

    public void Bind(
        ObservableObject target,
        IObjectMetadataProvider provider,
        IMemberAccessEvaluator accessEvaluator,
        InspectableAccessContext accessContext,
        IInspectorEditorRegistry editorRegistry)
    {
        if (_target is not null)
        {
            _target.LayoutInvalidated -= HandleLayoutInvalidated;
        }

        _target = target;
        _provider = provider;
        _accessEvaluator = accessEvaluator;
        _accessContext = accessContext;
        _editorRegistry = editorRegistry;
        _target.LayoutInvalidated += HandleLayoutInvalidated;
        RequestRebuild();
    }

    public void Clear()
    {
        if (_target is not null)
        {
            _target.LayoutInvalidated -= HandleLayoutInvalidated;
        }

        _target = null;
        _provider = null;
        _accessEvaluator = new DefaultMemberAccessEvaluator();
        _accessContext = InspectableAccessContext.Empty;
        _editorRegistry = new DefaultInspectorEditorRegistry();
        _pendingRebuild = false;
        RootPanel.Children.Clear();
    }

    private void HandleLayoutInvalidated(object? sender, EventArgs e) => RequestRebuild();

    private void RequestRebuild()
    {
        if (_isRebuilding)
        {
            _pendingRebuild = true;
            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (_target is null || _provider is null)
        {
            return;
        }

        try
        {
            _isRebuilding = true;
            RootPanel.Children.Clear();
            var metadata = _provider.GetMetadata(_target);

            // Runtime, generated, and reflected members all arrive in the same metadata shape here.
            // That lets the inspector stay ignorant of where the metadata originally came from.
            var orderedMembers = metadata.Members
                .Select(member => member with
                {
                    EffectiveAccess = _accessEvaluator.Evaluate(member, _target, _accessContext)
                })
                // Rejections are layout-level decisions owned by the model.
                // Access rules are user/session decisions owned by the evaluator.
                .Where(member => !_target.IsRejected(member.Key) && member.EffectiveAccess.CanView)
                .OrderBy(member => member.Section, StringComparer.OrdinalIgnoreCase)
                .ThenBy(member => member.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(member => member.Order)
                .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? currentSection = null;
            string? currentCategory = null;
            foreach (var member in orderedMembers)
            {
                if (!string.Equals(currentSection, member.Section, StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = member.Section;
                    currentCategory = null;
                    AddHeader(currentSection, 0, FontWeights.Bold, 1);
                }

                if (!string.Equals(currentCategory, member.Category, StringComparison.OrdinalIgnoreCase))
                {
                    currentCategory = member.Category;
                    AddHeader(currentCategory, 8, FontWeights.SemiBold, 0.82);
                }

                var control = _editorRegistry.CreateEditor(new InspectorEditorContext(
                    _target,
                    member,
                    _provider,
                    _accessEvaluator,
                    _accessContext,
                    _editorRegistry));

                if (control is not null)
                {
                    RootPanel.Children.Add(control);
                }
            }
        }
        finally
        {
            _isRebuilding = false;
            if (_pendingRebuild)
            {
                _pendingRebuild = false;
                Rebuild();
            }
        }
    }

    private void AddHeader(string? text, double topMargin, FontWeight fontWeight, double opacity)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        RootPanel.Children.Add(new TextBlock
        {
            Text = text,
            Margin = new Thickness(0, topMargin, 0, 4),
            FontSize = fontWeight == FontWeights.Bold ? 16 : 13,
            FontWeight = fontWeight,
            Opacity = opacity
        });
    }
}
