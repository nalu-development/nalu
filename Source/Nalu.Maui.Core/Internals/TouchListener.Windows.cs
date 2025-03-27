#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Nalu.Internals;

internal partial class TouchListener
{
    public TouchListener() { }

    partial void AttachView(UIElement view)
    {
        view.PointerPressed += OnWindowsPointerPressed;
        view.PointerMoved += OnWindowsPointerMoved;
        view.PointerReleased += OnWindowsPointerReleased;
    }

    partial void DetachView(UIElement view)
    {
        view.PointerPressed -= OnWindowsPointerPressed;
        view.PointerMoved -= OnWindowsPointerMoved;
        view.PointerReleased -= OnWindowsPointerReleased;
    }

    private void OnWindowsPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as UIElement);

        if (p is null) {
            return;
        }

        var args = new TouchEventArgs(new Point(p.Position.X, p.Position.Y));
        InvokePressed(args);

        if (!args.Propagates)
        {
            e.Handled = true;
        }
    }

    private void OnWindowsPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as UIElement);

        if (p is null) {
            return;
        }

        var args = new TouchEventArgs(new Point(p.Position.X, p.Position.Y));
        InvokeMoved(args);

        if (!args.Propagates)
        {
            e.Handled = true;
        }
    }

    private void OnWindowsPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(sender as UIElement);

        if (p is null) {
            return;
        }

        var args = new TouchEventArgs(new Point(p.Position.X, p.Position.Y));
        InvokeReleased(args);

        if (!args.Propagates)
        {
            e.Handled = true;
        }
    }
}
#endif
