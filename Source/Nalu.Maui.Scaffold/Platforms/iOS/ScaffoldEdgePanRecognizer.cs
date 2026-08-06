using CoreGraphics;
using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// The interactive-pop gesture: a plain pan with MANUAL edge gating.
/// <see cref="UIScreenEdgePanGestureRecognizer"/> is deliberately NOT used: measured on iOS 26
/// (simulator and device), its begin-time edge test consumes stale recognizer state and fails
/// erratically depending on where the previous touch ended, while a plain pan receives every
/// touch reliably. The caller gates on <see cref="StartedAtLeadingEdge"/> plus direction in
/// ShouldBegin.
/// </summary>
internal sealed class ScaffoldEdgePanRecognizer : UIPanGestureRecognizer
{
    private const double _edgeTolerance = 30;

    /// <summary>Whether the CURRENT touch sequence began within the leading-edge zone.</summary>
    public bool StartedAtLeadingEdge { get; private set; }

    public ScaffoldEdgePanRecognizer(Action<UIPanGestureRecognizer> action)
        : base(action)
    {
        MaximumNumberOfTouches = 1;
    }

    public override void TouchesBegan(NSSet touches, UIEvent evt)
    {
        // Read the position from the UITouch itself, NEVER from this recognizer's
        // LocationInView: at TouchesBegan time the recognizer's internal location still holds
        // the PREVIOUS gesture's end position (measured on iOS 26, simulator and device) -
        // gating on it makes every other swipe fail depending on where the last one released.
        var location = (touches.AnyObject as UITouch)?.LocationInView(View) ?? CGPoint.Empty;
        StartedAtLeadingEdge = location.X <= _edgeTolerance;
        base.TouchesBegan(touches, evt);
    }

    /// <summary>
    /// Hard-resets the recognizer's touch bookkeeping after a settled interactive pop, so the
    /// next edge swipe starts from a clean recognizer regardless of how the session ended.
    /// </summary>
    public void ResetTracking()
    {
        Enabled = false;
        Enabled = true;
    }
}
