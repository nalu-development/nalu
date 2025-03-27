#if ANDROID
using Android.Views;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu.Internals;

internal partial class TouchListener
{
    partial void AttachView(AView view) => view.Touch += OnTouched;

    partial void DetachView(AView view) => view.Touch -= OnTouched;

    private void OnTouched(object? sender, AView.TouchEventArgs e)
    {
        var view = sender as AView ?? throw new NullReferenceException("sender should be an Android.Views.View");
        var evt = e.Event!;

        var args = CreateArgsFromMotionEvent(view, evt);

        switch (evt.Action)
        {
#pragma warning disable CA1416
            case MotionEventActions.ButtonPress:
#pragma warning restore CA1416
            case MotionEventActions.Pointer1Down:
            case MotionEventActions.Pointer2Down:
            case MotionEventActions.Pointer3Down:
            case MotionEventActions.Down:
                InvokePressed(args);

                break;
            case MotionEventActions.Move:
            case MotionEventActions.HoverMove:
                InvokeMoved(args);

                break;
#pragma warning disable CA1416
            case MotionEventActions.ButtonRelease:
#pragma warning restore CA1416
            case MotionEventActions.Pointer1Up:
            case MotionEventActions.Pointer2Up:
            case MotionEventActions.Pointer3Up:
            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                InvokeReleased(args);

                break;
        }

        if (!args.Propagates)
        {
            view.Parent?.RequestDisallowInterceptTouchEvent(true);
        }
    }

    private static TouchEventArgs CreateArgsFromMotionEvent(AView view, MotionEvent evt)
    {
        var platX = evt.GetX();
        var platY = evt.GetY();
        var context = view.Context;
        var x = context.FromPixels(platX);
        var y = context.FromPixels(platY);
        var position = new Point(x, y);

        return new TouchEventArgs(position);
    }
}
#endif
