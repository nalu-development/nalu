#if IOS || MACCATALYST || ANDROID || WINDOWS
using Microsoft.Maui.Handlers;
using Nalu.Internals;

#if IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#elif ANDROID
using PlatformView = Android.Views.View;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#endif

namespace Nalu;

/// <summary>
/// Handler for the <see cref="ScrollBox" /> view.
/// </summary>
public partial class ScrollBoxHandler : ViewHandler<IScrollBox, PlatformView>
{
    /// <summary>
    /// The property mapper for the <see cref="IScrollBox" /> interface.
    /// </summary>
    public static readonly IPropertyMapper<IScrollBox, ScrollBoxHandler> Mapper =
        new PropertyMapper<IScrollBox, ScrollBoxHandler>(ViewMapper)
        {
            [nameof(IContentView.Content)] = MapContent,
            [nameof(IScrollBox.Orientation)] = MapOrientation,
            [nameof(IScrollBox.IsScrollEnabled)] = MapIsScrollEnabled,
            [nameof(IScrollBox.ScrollBarVisibility)] = MapScrollBarVisibility,
            [nameof(IScrollBox.FadingEdgeLength)] = MapFadingEdgeLength,
            [nameof(IScrollBox.SizingStrategy)] = MapSizingStrategy,
            [nameof(IScrollBox.IsRefreshEnabled)] = MapIsRefreshEnabled,
            [nameof(IScrollBox.IsRefreshing)] = MapIsRefreshing,
            [nameof(IScrollBox.RefreshAccentColor)] = MapRefreshAccentColor,
        };

    /// <summary>
    /// The command mapper for the <see cref="IScrollBox" /> interface.
    /// </summary>
    public static readonly CommandMapper<IScrollBox, ScrollBoxHandler> CommandMapper =
        new(ViewCommandMapper)
        {
            ["ScrollTo"] = MapScrollTo,
            ["SetScrollEventEnabled"] = MapSetScrollEventEnabled,
#if WINDOWS
            // WinUI does not propagate a measure invalidation from a parent down to its content:
            // invalidating only the ScrollViewer (the default mapper) leaves the content panel
            // measured with its previous content, so hugging never re-measures. MAUI's own
            // ScrollViewHandler carries the same override for the same reason.
            [nameof(IView.InvalidateMeasure)] = MapInvalidateMeasure,
#endif
        };

    /// <summary>
    /// A flag to skip expensive re-mapping work during initial setup.
    /// </summary>
    protected bool IsConnecting { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrollBoxHandler" /> class.
    /// </summary>
    public ScrollBoxHandler()
        : base(Mapper, CommandMapper)
    {
    }

    /// <inheritdoc />
    public override void SetVirtualView(IView view)
    {
        IsConnecting = true;
        base.SetVirtualView(view);
        IsConnecting = false;
    }

    #region SizingStrategy

    /// <summary>
    /// Extent changes below this (device-independent units) never reach the layout system.
    /// </summary>
    private const double _sizeToContentEpsilon = 0.5;

    /// <summary>The clamped extent last handed to the cross-platform layout.</summary>
    private double? _lastDesiredExtent;

    /// <summary>The scroll-axis constraint of the last measure pass (the room the parent offered).</summary>
    private double _lastMeasureConstraint = double.PositiveInfinity;

    /// <summary>Resets the sizing state and re-measures when the strategy changes.</summary>
    public static void MapSizingStrategy(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        handler._lastDesiredExtent = null;
        handler.UpdateFillViewport(scrollBox);

        if (!handler.IsConnecting)
        {
            scrollBox.InvalidateMeasure();
        }
    }

    /// <summary>
    /// Applies the strategy to a measured content extent: capped by <see cref="ScrollBoxSizingMode.Max" />
    /// and, either way, never larger than what the parent offered.
    /// </summary>
    private static double ClampExtent(double contentExtent, ScrollBoxSizingStrategy strategy, double constraint)
    {
        var extent = Math.Max(0, contentExtent);

        if (strategy.Mode == ScrollBoxSizingMode.Max)
        {
            extent = Math.Min(extent, strategy.MaxExtent);
        }

        if (!double.IsInfinity(constraint) && !double.IsNaN(constraint))
        {
            extent = Math.Min(extent, constraint);
        }

        return extent;
    }

    /// <inheritdoc />
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    {
        if (VirtualView is not { SizingStrategy.Mode: not ScrollBoxSizingMode.Fill } scrollBox)
        {
            // Fill measures nothing: the size offered by the parent is the size.
            return base.GetDesiredSize(widthConstraint, heightConstraint);
        }

        var strategy = scrollBox.SizingStrategy;
        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;
        var scrollConstraint = horizontal ? widthConstraint : heightConstraint;
        var crossConstraint = horizontal ? heightConstraint : widthConstraint;

        _lastMeasureConstraint = scrollConstraint;

        // A single content view is cheap to measure cross-platform (unlike virtualized content),
        // so the hugging modes measure it directly with an unbounded scroll axis.
        var measured = scrollBox.CrossPlatformMeasure(
            horizontal ? double.PositiveInfinity : widthConstraint,
            horizontal ? heightConstraint : double.PositiveInfinity
        );

        // Platforms whose scroller drives the content measure through its OWN measure pass need
        // that pass to actually happen: this override replaces the base one, which is where a
        // handler normally measures its platform view.
        MeasurePlatformScroller(
            horizontal ? double.PositiveInfinity : widthConstraint,
            horizontal ? heightConstraint : double.PositiveInfinity
        );

        var contentExtent = horizontal ? measured.Width : measured.Height;
        var extent = ClampExtent(contentExtent, strategy, scrollConstraint);
        _lastDesiredExtent = extent;

        // Hugging applies to the SCROLL axis only: the cross axis fills what the parent offered
        // (a vertical ScrollBox is as wide as its slot, not as wide as its content). Reporting
        // the content's natural cross size instead makes WinUI measure the content panel with
        // that width — and a narrower panel then re-measures the content, so the extent never
        // settles. Only an unbounded cross constraint falls back to the measured size.
        var cross = double.IsInfinity(crossConstraint) || double.IsNaN(crossConstraint)
            ? horizontal ? measured.Height : measured.Width
            : crossConstraint;

        return horizontal ? new Size(extent, cross) : new Size(cross, extent);
    }

    /// <summary>
    /// Asks for a cross-platform re-measure ONLY when the clamped extent actually moved — invoked
    /// by the platform partials after every content layout pass.
    /// </summary>
    /// <remarks>
    /// The invalidation is always DISPATCHED: this runs at the tail of a platform layout pass,
    /// and invalidating a measure from inside a layout pass is the classic self-sustaining loop.
    /// </remarks>
    private void RequestSizingMeasureIfNeeded(double contentWidth, double contentHeight)
    {
        if (VirtualView is not { SizingStrategy.Mode: not ScrollBoxSizingMode.Fill } scrollBox)
        {
            return;
        }

        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;
        var contentExtent = horizontal ? contentWidth : contentHeight;
        var extent = ClampExtent(contentExtent, scrollBox.SizingStrategy, _lastMeasureConstraint);

        if (_lastDesiredExtent is { } last && Math.Abs(last - extent) <= _sizeToContentEpsilon)
        {
            return;
        }

        _lastDesiredExtent = extent;

        if (VirtualView is VisualElement visualElement)
        {
            visualElement.Dispatcher.Dispatch(() =>
                {
                    if (VirtualView is { } view)
                    {
                        view.InvalidateMeasure();
                    }
                }
            );
        }
    }

    /// <summary>
    /// Reports a completed content layout pass to the control (updates <see cref="ScrollBox.ContentSize" />,
    /// flushes queued descendant scrolls) and re-evaluates the sizing strategy.
    /// </summary>
    private void OnContentLaidOut(double contentWidth, double contentHeight)
    {
        (VirtualView as IScrollBoxController)?.ContentLaidOut(contentWidth, contentHeight);
        RequestSizingMeasureIfNeeded(contentWidth, contentHeight);
    }

    /// <summary>
    /// Fill-viewport (content stretched to the viewport when smaller) applies only to
    /// <see cref="ScrollBoxSizingMode.Fill" />: a hugging box is content-sized by definition, and
    /// a viewport-stretched content wrapper would mask the content's real extent from the
    /// shrink-detection path. Platform partials implement the switch.
    /// </summary>
    private partial void UpdateFillViewport(IScrollBox scrollBox);

    /// <summary>
    /// Measures the platform scroller with the hugging constraints (scroll axis unbounded).
    /// </summary>
    /// <remarks>
    /// Only WinUI needs this: there the content panel is measured by the ScrollViewer's own
    /// measure pass, and this handler's <see cref="GetDesiredSize" /> override replaces the base
    /// implementation that would normally have measured the platform view — leaving the panel
    /// constrained to zero. The Apple and Android scrollers measure their content from their own
    /// layout callbacks, so their implementations are intentionally empty.
    /// </remarks>
    private partial void MeasurePlatformScroller(double widthConstraint, double heightConstraint);

    #endregion
}
#endif
