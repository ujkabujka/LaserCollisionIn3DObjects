namespace BaseFramework.Wpf.Controls;

public interface IInspectorEditorRegistry
{
    FrameworkElement? CreateEditor(InspectorEditorContext context);
}
