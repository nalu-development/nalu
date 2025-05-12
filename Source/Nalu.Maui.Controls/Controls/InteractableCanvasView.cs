using System.Diagnostics.CodeAnalysis;
using Nalu.Internals;
using SkiaSharp.Views.Maui.Controls;
using SkiaSharp.Views.Maui.Handlers;
#if MACCATALYST || IOS
#if NET9_0_OR_GREATER || MACCATALYST
using PlatformView = SkiaSharp.Views.iOS.SKCanvasView;

#else
using UIKit;
using PlatformView = object;
#endif

#elif ANDROID
using PlatformView = SkiaSharp.Views.Android.SKCanvasView;

#elif WINDOWS
using PlatformView = SkiaSharp.Views.Windows.SKXamlCanvas;

#else
using PlatformView = System.Object;
#endif

namespace Nalu;

/// <summary>
/// A touch enabled <see cref="SKCanvasView" />.
/// </summary>
public class InteractableCanvasView : SKCanvasView
{
    /// <summary>
    /// Invoked when the pointer/tap is pressed.
    /// </summary>
    [Experimental("NLU001")]
    protected virtual void OnTouchPressed(TouchEventArgs p) { }

    /// <summary>
    /// Invoked when the pointer/tap is released.
    /// </summary>
    [Experimental("NLU001")]
    protected virtual void OnTouchReleased(TouchEventArgs p) { }

    /// <summary>
    /// Invoked when the pointer/tap moves.
    /// </summary>
    [Experimental("NLU001")]
    protected virtual void OnTouchMoved(TouchEventArgs p) { }

#pragma warning disable NLU001
    internal void InvokePressed(TouchEventArgs args) => OnTouchPressed(args);
    internal void InvokeReleased(TouchEventArgs args) => OnTouchReleased(args);
    internal void InvokeMoved(TouchEventArgs args) => OnTouchMoved(args);
#pragma warning restore NLU001
}

internal class InteractableCanvasViewHandler : SKCanvasViewHandler
{
    private TouchListener? _touchListener;
    private new InteractableCanvasView? VirtualView => base.VirtualView as InteractableCanvasView;

    protected override void ConnectHandler(PlatformView platformView)
    {
        base.ConnectHandler(platformView);

        _touchListener = new TouchListener();
#if IOS && !NET9_0_OR_GREATER
        _touchListener.Attach((UIView) platformView);
#else
        _touchListener.Attach(platformView);
#endif
        _touchListener.Pressed += OnPressed;
        _touchListener.Released += OnReleased;
        _touchListener.Moved += OnMoved;
    }

    protected override void DisconnectHandler(PlatformView platformView)
    {
        if (_touchListener is not null)
        {
#if IOS && !NET9_0_OR_GREATER
            _touchListener.Detach((UIView) platformView);
#else
            _touchListener.Detach(platformView);
#endif
            _touchListener.Pressed -= OnPressed;
            _touchListener.Released -= OnReleased;
            _touchListener.Moved -= OnMoved;
            _touchListener = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnPressed(object? sender, TouchEventArgs e) => VirtualView?.InvokePressed(e);

    private void OnReleased(object? sender, TouchEventArgs e) => VirtualView?.InvokeReleased(e);

    private void OnMoved(object? sender, TouchEventArgs e) => VirtualView?.InvokeMoved(e);
}
