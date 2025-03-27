#if IOS || MACCATALYST
using PlatformView = UIKit.UIView;

#elif ANDROID
using PlatformView = Android.Views.View;

#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.UIElement;

#else
using PlatformView = System.Object;
#endif

namespace Nalu.Internals;

internal partial class TouchListener
{
    private PlatformView? _platformView;

    /// <summary>
    /// Called when the pointer/tap is pressed.
    /// </summary>
    public event EventHandler<TouchEventArgs>? Pressed;

    /// <summary>
    /// Called when the pointer/tap is released.
    /// </summary>
    public event EventHandler<TouchEventArgs>? Released;

    /// <summary>
    /// Called when the pointer/tap moves.
    /// </summary>
    public event EventHandler<TouchEventArgs>? Moved;

    public void Attach(PlatformView view)
    {
        _platformView = view;
        AttachView(view);
    }

    public void Detach(PlatformView view)
    {
        _platformView = null;
        DetachView(view);
    }

    partial void AttachView(PlatformView view);
    partial void DetachView(PlatformView view);

    private void InvokePressed(TouchEventArgs args) => Pressed?.Invoke(_platformView, args);
    private void InvokeReleased(TouchEventArgs args) => Released?.Invoke(_platformView, args);
    private void InvokeMoved(TouchEventArgs args) => Moved?.Invoke(_platformView, args);
}
