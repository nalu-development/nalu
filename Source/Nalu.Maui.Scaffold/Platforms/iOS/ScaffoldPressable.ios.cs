using Foundation;
using UIKit;

namespace Nalu.Internals;

internal static partial class ScaffoldPressable
{
    static partial void PlatformAttach(View touchSurface, Action onPressed)
    {
        if (touchSurface.Handler?.PlatformView is not UIView platformView)
        {
            return;
        }

        // Idempotent across handler reconnections to the same platform view.
        if (platformView.GestureRecognizers?.Any(static r => r is PressObserverRecognizer) == true)
        {
            return;
        }

        platformView.AddGestureRecognizer(new PressObserverRecognizer(onPressed));
    }

    /// <summary>
    /// A purely observational recognizer: reports touch-down and immediately fails, so it can
    /// never delay or cancel the MAUI tap recognizer living on an ancestor.
    /// </summary>
    private sealed class PressObserverRecognizer : UIGestureRecognizer
    {
        private readonly Action _onPressed;

        public PressObserverRecognizer(Action onPressed)
        {
            _onPressed = onPressed;
            CancelsTouchesInView = false;
            DelaysTouchesBegan = false;
            DelaysTouchesEnded = false;
            ShouldRecognizeSimultaneously = static (_, _) => true;
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            base.TouchesBegan(touches, evt);
            _onPressed();
            State = UIGestureRecognizerState.Failed;
        }
    }
}
