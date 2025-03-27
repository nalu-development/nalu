#if IOS || MACCATALYST
using System.Diagnostics.CodeAnalysis;
using CoreGraphics;
using Foundation;
using UIKit;

// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable RedundantOverriddenMember

namespace Nalu.Internals;

internal partial class TouchListener
{
    private readonly ControlledTouchGestureRecognizer _gestureRecognizer;

    public TouchListener()
    {
        _gestureRecognizer = new ControlledTouchGestureRecognizer(OnLongPress);
    }

    partial void AttachView(UIView view) => view.AddGestureRecognizer(_gestureRecognizer);

    partial void DetachView(UIView view) => view.RemoveGestureRecognizer(_gestureRecognizer);

    private void OnLongPress(NativeTouchEvent e)
    {
        var location = e.Location;
        var p = new Point(location.X, location.Y);
        var args = new TouchEventArgs(p);

        switch (e.State)
        {
            case UIGestureRecognizerState.Began:
                InvokePressed(args);

                break;
            case UIGestureRecognizerState.Changed:
                InvokeMoved(args);

                break;
            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Ended:
                InvokeReleased(args);

                break;
        }

        e.Propagates = args.Propagates;
    }
}

internal class NativeTouchEvent
{
    public bool Propagates { get; set; } = true;

    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public required UIEvent Event { get; init; }
    public required UIGestureRecognizerState State { get; init; }
    public required CGPoint Location { get; init; }
}

// ReSharper disable once InconsistentNaming
[SuppressMessage("Style", "IDE0022:Use expression body for method")]
internal class ControlledTouchGestureRecognizer : UIGestureRecognizer
{
    private readonly Action<NativeTouchEvent> _action;

    public ControlledTouchGestureRecognizer(Action<NativeTouchEvent> action)
    {
        _action = action;
    }

    public override void TouchesBegan(NSSet touches, UIEvent evt)
    {
        base.TouchesBegan(touches, evt);
        var args = CreateNativeTouchEvent(evt, UIGestureRecognizerState.Began);
        _action(args);

        if (!args.Propagates)
        {
            State = UIGestureRecognizerState.Began;
        }
    }

    public override void TouchesMoved(NSSet touches, UIEvent evt)
    {
        base.TouchesMoved(touches, evt);
        var args = CreateNativeTouchEvent(evt, UIGestureRecognizerState.Changed);
        _action(args);

        if (!args.Propagates || State == UIGestureRecognizerState.Began)
        {
            State = UIGestureRecognizerState.Changed;
        }
    }

    public override void TouchesCancelled(NSSet touches, UIEvent evt)
    {
        base.TouchesCancelled(touches, evt);
        var args = CreateNativeTouchEvent(evt, UIGestureRecognizerState.Cancelled);
        _action(args);

        if (!args.Propagates || State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed)
        {
            State = UIGestureRecognizerState.Cancelled;
        }
    }

    public override void TouchesEnded(NSSet touches, UIEvent evt)
    {
        base.TouchesEnded(touches, evt);
        var args = CreateNativeTouchEvent(evt, UIGestureRecognizerState.Ended);
        _action(args);

        if (!args.Propagates || State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed)
        {
            State = UIGestureRecognizerState.Ended;
        }
    }

    private NativeTouchEvent CreateNativeTouchEvent(UIEvent evt, UIGestureRecognizerState state)
    {
        var args = new NativeTouchEvent
                   {
                       Event = evt,
                       State = state,
                       Location = LocationInView(View)
                   };

        return args;
    }
}

#endif
