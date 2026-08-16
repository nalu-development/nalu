using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// iOS host of a popup's or bottom sheet's platform view: a container-sized, input-transparent
/// layer holding the panel exactly where the presenter arranged it (container coordinates —
/// no geometry of its own), whose only job is to TERMINATE the platform measure-invalidation
/// walk MAUI runs up the native chain when any descendant's size requirement changes (an image
/// that finishes loading, an expanding section) and hand that signal to the presenter, which
/// re-places the overlay from its new natural size — the same typed channel
/// (<see cref="IPlatformMeasureInvalidationController"/>) the chrome strips use. It is one of two
/// signals: MAUI gates this walk per view (a view that already propagated stays silent until it
/// is measured again), so the presenter also listens to the Controls-level
/// <see cref="VisualElement.MeasureInvalidated"/> of the root, which bubbles by default (unless the
/// app opts into <c>SkipMeasureInvalidatedPropagation</c> — then this host is the only channel).
/// </summary>
internal sealed class ScaffoldOverlayPanelHost : UIView, IPlatformMeasureInvalidationController
{
    private Action? _contentMeasureInvalidated;
    private bool _pendingInvalidation;
    private bool _invalidateOnWindow;

    /// <summary>
    /// Raised (synchronously, inside the invalidation walk) when the hosted panel or any of its
    /// descendants invalidated its measure. An invalidation that arrives before the callback is
    /// attached (the mount itself) is latched and delivered on attach: the presenter MUST
    /// re-measure after every signal — MAUI's intermediate views block further propagation
    /// until they are measured again, so a swallowed signal would silence the chain for good.
    /// </summary>
    public Action? ContentMeasureInvalidated
    {
        get => _contentMeasureInvalidated;
        set
        {
            _contentMeasureInvalidated = value;

            if (value is not null && _pendingInvalidation)
            {
                _pendingInvalidation = false;
                value();
            }
        }
    }

    public ScaffoldOverlayPanelHost(UIView panel, CGRect bounds)
    {
        Frame = bounds;
        AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        BackgroundColor = UIColor.Clear;
        AddSubview(panel);
    }

    /// <summary>The panel is the only thing that receives touches; the host itself is transparent (the scrim below handles the rest).</summary>
    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        var hit = base.HitTest(point, uievent);

        return ReferenceEquals(hit, this) ? null : hit;
    }

    /// <returns><c>false</c>: propagation stops here — the presenter owns the panel's placement.</returns>
    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        SetNeedsLayout();

        if (_contentMeasureInvalidated is { } callback)
        {
            callback();
        }
        else
        {
            _pendingInvalidation = true;
        }

        return false;
    }

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow()
        => _invalidateOnWindow = true;

    public override void MovedToWindow()
    {
        base.MovedToWindow();

        if (_invalidateOnWindow && Window is not null)
        {
            _invalidateOnWindow = false;
            ((IPlatformMeasureInvalidationController)this).InvalidateMeasure(isPropagating: true);
        }
    }
}
