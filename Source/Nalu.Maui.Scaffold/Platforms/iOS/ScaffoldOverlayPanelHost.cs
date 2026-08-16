using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// iOS host of a popup's or bottom sheet's platform view: a container-sized, input-transparent
/// layer holding the panel exactly where the presenter arranged it (container coordinates —
/// no geometry of its own). It is the layout ROOT of the overlay subtree: MAUI's platform
/// measure-invalidation walk (<see cref="IPlatformMeasureInvalidationController"/>, run up the
/// native chain when any descendant's size requirement changes — an image that finishes
/// loading, an expanding section) TERMINATES here, marking the panel dirty and requesting a
/// layout; the measure + arrange then run in <see cref="LayoutSubviews"/> — the measure pass —
/// through the presenter (<see cref="MeasurePanel"/>), never inside the invalidation itself.
/// Same discipline as the chrome strips.
/// </summary>
internal sealed class ScaffoldOverlayPanelHost : UIView, IPlatformMeasureInvalidationController
{
    private bool _panelDirty;
    private bool _invalidateOnWindow;

    /// <summary>
    /// The presenter's measure + arrange of the hosted panel, run from <see cref="LayoutSubviews"/>
    /// while the panel is dirty (a dirty panel waits for the callback to be attached).
    /// </summary>
    public Action? MeasurePanel { get; set; }

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

    /// <summary>Marks the panel dirty and requests a layout — no measuring here.</summary>
    /// <returns><c>false</c>: propagation stops here — the presenter owns the panel's placement.</returns>
    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        _panelDirty = true;
        SetNeedsLayout();

        return false;
    }

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow()
        => _invalidateOnWindow = true;

    /// <inheritdoc />
    public override void LayoutSubviews()
    {
        // Measure first: the presenter measures the panel's natural size and arranges it (frame,
        // translation) — the subviews then lay out from that geometry.
        if (_panelDirty && MeasurePanel is { } measure)
        {
            _panelDirty = false;
            measure();
        }

        base.LayoutSubviews();
    }

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
