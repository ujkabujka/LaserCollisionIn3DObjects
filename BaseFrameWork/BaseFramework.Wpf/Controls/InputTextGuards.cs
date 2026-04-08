using System.Windows.Input;

namespace BaseFramework.Wpf.Controls;

internal static class InputTextGuards
{
    internal static string BuildProposedText(TextBox textBox, string input)
    {
        var text = textBox.Text ?? string.Empty;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        return text.Remove(selectionStart, selectionLength).Insert(selectionStart, input);
    }

    internal static void BlockInvalidTyping(TextBox textBox, TextCompositionEventArgs e, Func<string, bool> isValid)
    {
        var proposed = BuildProposedText(textBox, e.Text);
        e.Handled = !isValid(proposed);
    }

    internal static void BlockInvalidPaste(TextBox textBox, DataObjectPastingEventArgs e, Func<string, bool> isValid)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var proposed = BuildProposedText(textBox, pasted);
        if (!isValid(proposed))
        {
            e.CancelCommand();
        }
    }
}

