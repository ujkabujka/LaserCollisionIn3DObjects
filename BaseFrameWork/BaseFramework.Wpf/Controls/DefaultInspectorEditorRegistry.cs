using BaseFramework.Core;
using BaseFramework.Core.Access;
using BaseFramework.Core.Metadata;

namespace BaseFramework.Wpf.Controls;

public sealed class DefaultInspectorEditorRegistry : IInspectorEditorRegistry
{
    private readonly Dictionary<string, Func<InspectorEditorContext, FrameworkElement?>> _hintFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<MemberKind, Func<InspectorEditorContext, FrameworkElement?>> _kindFactories = new();

    public DefaultInspectorEditorRegistry()
    {
        Register(MemberKind.Integer, context => new IntegerEditorControl(context.Target, context.Member));
        Register(MemberKind.Double, context => new DoubleEditorControl(context.Target, context.Member));
        Register(MemberKind.Enum, context => new EnumEditorControl(context.Target, context.Member));
        Register(MemberKind.Selection, context => new StringEditorControl(context.Target, context.Member));
        Register(MemberKind.Boolean, context => new BooleanEditorControl(context.Target, context.Member));
        Register(MemberKind.String, context => new StringEditorControl(context.Target, context.Member));
        Register(MemberKind.MultiLineText, context => new StringEditorControl(context.Target, context.Member));
        Register(MemberKind.Note, context => new NoteEditorControl(context.Target, context.Member));
        Register(MemberKind.DateTime, context => new DateTimeEditorControl(context.Target, context.Member));
        Register(MemberKind.Class, context =>
        {
            if (MemberAccess.GetValue(context.Member, context.Target) is not ObservableObject child)
            {
                return null;
            }

            return new ClassEditorControl(
                context.Member.DisplayName,
                child,
                context.MetadataProvider,
                context.Target,
                context.Member,
                context.AccessEvaluator,
                context.AccessContext,
                context.EditorRegistry);
        });
        Register(MemberKind.Collection, context => new CollectionEditorControl(
            context.Target,
            context.Member,
            context.MetadataProvider,
            context.AccessEvaluator,
            context.AccessContext,
            context.EditorRegistry));
        Register(MemberKind.Method, context => new MethodEditorControl(
            context.Target,
            context.Member,
            context.MetadataProvider,
            context.AccessEvaluator,
            context.AccessContext,
            context.EditorRegistry));
        Register(MemberKind.File, context => new PathPickerEditorControl(context.Target, context.Member, false));
        Register(MemberKind.Image, context => new PathPickerEditorControl(context.Target, context.Member, true));
        Register(MemberKind.Table, context => new TableEditorControl(context.Target, context.Member));

        Register(EditorHints.File, context => new PathPickerEditorControl(context.Target, context.Member, false));
        Register(EditorHints.Image, context => new PathPickerEditorControl(context.Target, context.Member, true));
        Register(EditorHints.Table, context => new TableEditorControl(context.Target, context.Member));
    }

    public FrameworkElement? CreateEditor(InspectorEditorContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Member.EditorHint) &&
            _hintFactories.TryGetValue(context.Member.EditorHint, out var hintedFactory))
        {
            return hintedFactory(context);
        }

        return _kindFactories.TryGetValue(context.Member.Kind, out var factory)
            ? factory(context)
            : null;
    }

    public void Register(MemberKind kind, Func<InspectorEditorContext, FrameworkElement?> factory)
        => _kindFactories[kind] = factory;

    public void Register(string editorHint, Func<InspectorEditorContext, FrameworkElement?> factory)
        => _hintFactories[editorHint] = factory;
}
