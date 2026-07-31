using UIKit;
#if NET10_0_OR_GREATER
using Microsoft.Maui.Platform;
#endif

namespace Nalu;

/// <summary>
/// Cell content host whose <see cref="NeedsMeasure"/> flag distinguishes genuine MAUI content
/// measure invalidations from UIKit layout side effects.
/// </summary>
/// <remarks>
/// The distinction is what prevents a main-thread livelock: UIKit (re)assigning the cell frame
/// during a collection view layout pass raises SetNeedsLayout too, and treating that as "content
/// changed" makes <see cref="VirtualScrollCollectionView"/> feed the compositional layout a
/// generic invalidation which resets the recorded preferred size back to the ESTIMATE — which
/// re-assigns the frame, which raises the flag again, forever (~1 relayout per frame, run loop
/// starved, app looks dead with no crash report).
/// <para>
/// On .NET 10+ MAUI propagates measure invalidations through
/// <c>IPlatformMeasureInvalidationController</c>, giving us an explicit channel: SetNeedsLayout
/// stays a plain layout request. On .NET 9 the interface is internal to MAUI, so we keep the
/// SetNeedsLayout inference but suppress it while the owning collection view is laying out —
/// frame side effects only happen inside that window, while genuine content changes (bindings,
/// user code) always originate outside it on the main thread.
/// </para>
/// </remarks>
internal class VirtualScrollCellContent : UIView
#if NET10_0_OR_GREATER
    , IPlatformMeasureInvalidationController
#endif
{
    // See https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/iOS/ViewExtensions.cs#L1023
    private const nint _nativeViewControlledByCrossPlatformLayout = 0x63D2A1;

    public bool NeedsMeasure { get; set; }

    public VirtualScrollCellContent()
    {
        Tag = _nativeViewControlledByCrossPlatformLayout;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        NeedsMeasure = false;
    }

#if !NET10_0_OR_GREATER
    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();

        // Ignore SetNeedsLayout raised while the collection view is laying out: those are frame
        // assignment side effects, not content changes (see the class remarks). On .NET 10+ this
        // inference does not exist at all — genuine invalidations arrive exclusively through
        // InvalidateMeasure, and SetNeedsLayout stays a plain layout request.
        if (Superview is VirtualScrollCell { Superview: VirtualScrollCollectionView { IsPerformingLayout: true } })
        {
            return;
        }

        NeedsMeasure = true;
        (Superview as VirtualScrollCell)?.OnContentMeasureInvalidated();
    }
#endif

#if NET10_0_OR_GREATER
    private bool _invalidateParentWhenMovedToWindow;

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow() => _invalidateParentWhenMovedToWindow = true;

    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        NeedsMeasure = true;
        SetNeedsLayout();
        (Superview as VirtualScrollCell)?.OnContentMeasureInvalidated();

        // Continue propagating so the collection view hears the invalidation too (it stops it).
        return true;
    }

    /// <inheritdoc/>
    public override void MovedToWindow()
    {
        base.MovedToWindow();

        if (_invalidateParentWhenMovedToWindow)
        {
            _invalidateParentWhenMovedToWindow = false;
            VirtualScrollViewExtensionsProxy.InvalidateAncestorsMeasures(this);
        }
    }
#endif
}
