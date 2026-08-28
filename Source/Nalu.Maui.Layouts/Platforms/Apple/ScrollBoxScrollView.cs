using CoreGraphics;
using UIKit;
#if NET10_0_OR_GREATER
using Microsoft.Maui.Platform;
#endif

namespace Nalu;

/// <summary>
/// The ScrollBox platform scroller on iOS/Mac Catalyst: a plain <see cref="UIScrollView" />
/// driving the MAUI cross-platform measure/arrange of its single content wrapper.
/// </summary>
/// <remarks>
/// <para>
/// Safe area handling is split by axis: along the scrolling axis UIKit's automatic
/// <c>adjustedContentInset</c> does the positional work (content rests inside the bars and
/// scrolls under them); on the cross axis — where <c>contentInsetAdjustmentBehavior</c> is not
/// reliable for non-scrollable directions — the content wrapper is positioned inside
/// <see cref="UIView.SafeAreaInsets" /> explicitly.
/// </para>
/// <para>
/// <see cref="LayoutSubviews" /> runs on every scrolled frame, so the measure/arrange pass is
/// gated: it only executes when the content invalidated its measure
/// (<see cref="ScrollBoxContentView.NeedsMeasure" />) or the geometry inputs changed.
/// </para>
/// </remarks>
internal sealed class ScrollBoxScrollView : UIScrollView
#if NET10_0_OR_GREATER
    , IPlatformMeasureInvalidationController
#endif
{
    private double _lastAvailableWidth = -1;
    private double _lastAvailableHeight = -1;
    private double _lastFillExtent = -1;

    public ICrossPlatformLayout? CrossPlatformLayout { get; set; }

    public ScrollBoxContentView? ContentWrapper { get; set; }

    public ScrollBoxOrientation Orientation { get; set; }

    /// <summary>Whether short content is stretched to the viewport (Fill sizing strategy only).</summary>
    public bool FillViewportEnabled { get; set; } = true;

    /// <summary>Invoked after every content measure/arrange pass with the canvas size in points.</summary>
    public Action<double, double>? ContentLaidOut { get; set; }

#if !NET10_0_OR_GREATER
    /// <summary>
    /// True while this scroll view is inside <see cref="LayoutSubviews" />. Used by
    /// <see cref="ScrollBoxContentView" /> on .NET 9 (where MAUI's
    /// IPlatformMeasureInvalidationController is internal) to tell frame-assignment
    /// SetNeedsLayout side effects apart from genuine content measure invalidations.
    /// </summary>
    internal bool IsPerformingLayout { get; private set; }
#endif

    public ScrollBoxScrollView()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(13) || OperatingSystem.IsMacCatalystVersionAtLeast(13, 1))
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            AutomaticallyAdjustsScrollIndicatorInsets = true;
        }

        DirectionalLockEnabled = true;
    }

    /// <summary>Forces a full measure/arrange on the next layout pass.</summary>
    public void InvalidateContentMeasure()
    {
        if (ContentWrapper is { } wrapper)
        {
            wrapper.NeedsMeasure = true;
        }

        SetNeedsLayout();
    }

    /// <inheritdoc />
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        if (CrossPlatformLayout is not { } crossPlatformLayout || ContentWrapper is not { } wrapper)
        {
            return;
        }

        var bounds = Bounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // The SAFE AREA (stable) bounds the cross axis and the fill extent; the scroll axis is
        // ruled by adjustedContentInset at scroll time. Using adjustedContentInset here would
        // couple the measure inputs to transient inset changes (e.g. UIRefreshControl expansion).
        var safeArea = SafeAreaInsets;
        var horizontal = Orientation == ScrollBoxOrientation.Horizontal;
        var availableWidth = Math.Max(0, bounds.Width - safeArea.Left - safeArea.Right);
        var availableHeight = Math.Max(0, bounds.Height - safeArea.Top - safeArea.Bottom);
        var fillExtent = horizontal ? availableWidth : availableHeight;

        if (!wrapper.NeedsMeasure
            && Math.Abs(_lastAvailableWidth - availableWidth) < 0.5
            && Math.Abs(_lastAvailableHeight - availableHeight) < 0.5
            && Math.Abs(_lastFillExtent - fillExtent) < 0.5)
        {
            return;
        }

        _lastAvailableWidth = availableWidth;
        _lastAvailableHeight = availableHeight;
        _lastFillExtent = fillExtent;

        double contentWidth, contentHeight;

#if !NET10_0_OR_GREATER
        IsPerformingLayout = true;

        try
        {
#endif
        var measured = horizontal
            ? crossPlatformLayout.CrossPlatformMeasure(double.PositiveInfinity, availableHeight)
            : crossPlatformLayout.CrossPlatformMeasure(availableWidth, double.PositiveInfinity);

        if (horizontal)
        {
            contentHeight = availableHeight;
            contentWidth = FillViewportEnabled ? Math.Max(measured.Width, fillExtent) : measured.Width;
        }
        else
        {
            contentWidth = availableWidth;
            contentHeight = FillViewportEnabled ? Math.Max(measured.Height, fillExtent) : measured.Height;
        }

        crossPlatformLayout.CrossPlatformArrange(new Rect(0, 0, contentWidth, contentHeight));

        // The wrapper is offset by the CROSS-axis safe area (UIKit only owns the scroll axis).
        wrapper.Frame = horizontal
            ? new CGRect(0, safeArea.Top, contentWidth, contentHeight)
            : new CGRect(safeArea.Left, 0, contentWidth, contentHeight);

        wrapper.NeedsMeasure = false;

        var newContentSize = horizontal
            ? new CGSize(contentWidth, bounds.Height)
            : new CGSize(bounds.Width, contentHeight);

        if (ContentSize != newContentSize)
        {
            ContentSize = newContentSize;
        }
#if !NET10_0_OR_GREATER
        }
        finally
        {
            IsPerformingLayout = false;
        }
#endif

        ContentLaidOut?.Invoke(contentWidth, contentHeight);
    }

#if NET10_0_OR_GREATER
    private bool _invalidateParentWhenMovedToWindow;

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow() => _invalidateParentWhenMovedToWindow = true;

    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        SetNeedsLayout();

        return !isPropagating;
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
