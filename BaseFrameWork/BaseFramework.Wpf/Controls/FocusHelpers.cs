using System;
using System.Windows.Input;

namespace BaseFramework.Wpf.Controls;

internal static class FocusHelpers
{
    public static void AttachEnterToCommitAndMoveFocus(UIElement element, Action? commitAction = null)
    {
        element.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            commitAction?.Invoke();
            var request = new TraversalRequest(FocusNavigationDirection.Next);
            if (!element.MoveFocus(request))
            {
                Keyboard.ClearFocus();
            }

            e.Handled = true;
        };
    }
}

