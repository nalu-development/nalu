using AView = Android.Views.View;

namespace Nalu.Internals;

internal static partial class ScaffoldPressable
{
    static partial void PlatformAttach(View touchSurface, Action onPressed)
    {
        if (touchSurface.Handler?.PlatformView is AView platformView)
        {
            // The surface carries no MAUI gestures, so its OnTouchListener slot is free.
            // Returning false keeps the touch stream flowing to the ancestor that does.
            platformView.SetOnTouchListener(new PressDownListener(onPressed));
        }
    }

    private sealed class PressDownListener(Action onPressed) : Java.Lang.Object, AView.IOnTouchListener
    {
        public bool OnTouch(AView? v, Android.Views.MotionEvent? e)
        {
            if (e?.ActionMasked == Android.Views.MotionEventActions.Down)
            {
                onPressed();
            }

            return false;
        }
    }
}
