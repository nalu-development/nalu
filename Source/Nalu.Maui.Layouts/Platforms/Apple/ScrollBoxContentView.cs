using UIKit;
#if NET10_0_OR_GREATER
using System.Reflection;
using Microsoft.Maui.Platform;
using ViewExtensions = Microsoft.Maui.Platform.ViewExtensions;
#endif

namespace Nalu;

#if NET10_0_OR_GREATER
/// <summary>
/// Access to MAUI's internal measure-invalidation propagation helper.
/// </summary>
internal static class ScrollBoxViewExtensionsProxy
{
    private static readonly Action<UIView> _invalidateAncestorsMeasures = typeof(ViewExtensions)
                                                                          .GetMethod("InvalidateAncestorsMeasures", BindingFlags.Static | BindingFlags.NonPublic)!
                                                                          .CreateDelegate<Action<UIView>>();

    public static void InvalidateAncestorsMeasures(UIView view) => _invalidateAncestorsMeasures(view);
}
#endif

/// <summary>
/// Content host of the ScrollBox scroll view whose <see cref="NeedsMeasure" /> flag distinguishes
/// genuine MAUI content measure invalidations from UIKit layout side effects.
/// </summary>
/// <remarks>
/// Same protocol as VirtualScroll's <c>VirtualScrollCellContent</c>: on .NET 10+ MAUI propagates
/// measure invalidations through <c>IPlatformMeasureInvalidationController</c>, giving an explicit
/// channel; on .NET 9 the interface is internal to MAUI, so <see cref="UIView.SetNeedsLayout" />
/// is inferred to be a content change unless it happens while the owning scroll view is laying
/// out (frame-assignment side effects only occur inside that window). This distinction is what
/// prevents the measure-invalidate-measure livelock.
/// </remarks>
internal sealed class ScrollBoxContentView : UIView
#if NET10_0_OR_GREATER
    , IPlatformMeasureInvalidationController
#endif
{
    // See https://github.com/dotnet/maui/blob/main/src/Core/src/Platform/iOS/ViewExtensions.cs
    private const nint _nativeViewControlledByCrossPlatformLayout = 0x63D2A1;

    public bool NeedsMeasure { get; set; } = true;

    public ScrollBoxContentView()
    {
        Tag = _nativeViewControlledByCrossPlatformLayout;
    }

#if !NET10_0_OR_GREATER
    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();

        // Ignore SetNeedsLayout raised while the scroll view is laying out: those are frame
        // assignment side effects, not content changes (see the class remarks).
        if (Superview is ScrollBoxScrollView { IsPerformingLayout: true })
        {
            return;
        }

        NeedsMeasure = true;
        Superview?.SetNeedsLayout();
    }
#endif

#if NET10_0_OR_GREATER
    private bool _invalidateParentWhenMovedToWindow;

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow() => _invalidateParentWhenMovedToWindow = true;

    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        NeedsMeasure = true;
        SetNeedsLayout();
        Superview?.SetNeedsLayout();

        // Continue propagating so the scroll view hears the invalidation too (it stops it).
        return true;
    }

    /// <inheritdoc />
    public override void MovedToWindow()
    {
        base.MovedToWindow();

        if (_invalidateParentWhenMovedToWindow)
        {
            _invalidateParentWhenMovedToWindow = false;
            ScrollBoxViewExtensionsProxy.InvalidateAncestorsMeasures(this);
        }
    }
#endif
}
