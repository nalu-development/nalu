using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Nalu.Internals;

// The WinUI controls namespace is deliberately NOT imported wholesale: it makes
// ScrollBarVisibility and ScrollMode ambiguous with the MAUI types of the same name.
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
using ScrollViewer = Microsoft.UI.Xaml.Controls.ScrollViewer;
using ScrollViewerViewChangedEventArgs = Microsoft.UI.Xaml.Controls.ScrollViewerViewChangedEventArgs;
using WScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility;
using WScrollMode = Microsoft.UI.Xaml.Controls.ScrollMode;

namespace Nalu;

#pragma warning disable IDE0060
// ReSharper disable UnusedParameter.Local

/// <summary>
/// Locally-defined partial subclass of <see cref="ContentPanel" /> (same pattern as
/// <c>ViewBoxPanel</c>); the csproj enables AllowUnsafeBlocks on Windows so CsWinRT can
/// generate the ABI marshalling code for it (CsWinRT1030).
/// </summary>
internal partial class ScrollBoxContentPanel : ContentPanel;

/// <summary>
/// Handler for the <see cref="ScrollBox" /> view on Windows.
/// </summary>
/// <remarks>
/// Pull-to-refresh and the fading edge are not supported on Windows: the properties are accepted
/// but inactive, mirroring Nalu.Maui.VirtualScroll.
/// </remarks>
public partial class ScrollBoxHandler
{
    private ScrollViewer? _scrollViewer;
    private ContentPanel? _contentPanel;
    private bool _wasScrolling;
    private ScrollBoxScrollToRequest? _pendingScrollToRequest;
    private ScrollBoxScrollToRequest? _activeScrollToRequest;
    private bool _scrollEventsEnabled;

    /// <inheritdoc />
    protected override PlatformView CreatePlatformView()
    {
        _contentPanel = new ScrollBoxContentPanel
        {
            CrossPlatformLayout = VirtualView
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _contentPanel
        };

        _scrollViewer.Loaded += OnScrollViewerLoaded;
        _contentPanel.SizeChanged += OnContentPanelSizeChanged;

        return _scrollViewer;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        _pendingScrollToRequest?.Complete();
        _pendingScrollToRequest = null;
        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;

        if (_scrollViewer is not null)
        {
            _scrollViewer.Loaded -= OnScrollViewerLoaded;
            _scrollViewer.ViewChanged -= OnScrollViewerViewChanged;
        }

        if (_contentPanel is not null)
        {
            _contentPanel.SizeChanged -= OnContentPanelSizeChanged;
        }

        _contentPanel = null;
        _scrollViewer = null;

        base.DisconnectHandler(platformView);
    }

    private void OnScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            return;
        }

        scrollViewer.Loaded -= OnScrollViewerLoaded;
        scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.VerticalOffsetProperty, OnScrollOffsetChanged);
        scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.HorizontalOffsetProperty, OnScrollOffsetChanged);
        scrollViewer.ViewChanged += OnScrollViewerViewChanged;

        // Requests issued before the viewer was loaded were queued: this is their moment.
        if (_pendingScrollToRequest is { } pending)
        {
            _pendingScrollToRequest = null;
            ExecuteScrollToRequest(pending);
        }
    }

    private (double ScrollX, double ScrollY, double TotalWidth, double TotalHeight) GetScrollValues(ScrollViewer scrollViewer)
        => (
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset,
            scrollViewer.ExtentWidth,
            scrollViewer.ExtentHeight
        );

    private void OnScrollOffsetChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            return;
        }

        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scrollViewer);
        var controller = VirtualView as IScrollBoxController;

        if (!_wasScrolling)
        {
            _wasScrolling = true;
            controller?.ScrollStarted(scrollX, scrollY, totalWidth, totalHeight);
        }

        if (_scrollEventsEnabled)
        {
            controller?.Scrolled(scrollX, scrollY, totalWidth, totalHeight);
        }
    }

    private void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_scrollViewer is not { } scrollViewer || e.IsIntermediate)
        {
            return;
        }

        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scrollViewer);

        if (_wasScrolling)
        {
            _wasScrolling = false;
            (VirtualView as IScrollBoxController)?.ScrollEnded(scrollX, scrollY, totalWidth, totalHeight);
        }
        else
        {
            (VirtualView as ScrollBox)?.UpdateScrollPosition(scrollX, scrollY);
        }

        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;
    }

    private void OnContentPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnContentLaidOut(e.NewSize.Width, e.NewSize.Height);

        if (_pendingScrollToRequest is { } pending && _scrollViewer is { IsLoaded: true, ActualWidth: > 0 })
        {
            _pendingScrollToRequest = null;
            ExecuteScrollToRequest(pending);
        }
    }

    internal ScrollBoxGeometry? GetGeometry()
    {
        if (_scrollViewer is not { } scrollViewer || _contentPanel is not { } panel)
        {
            return null;
        }

        return new ScrollBoxGeometry(
            scrollViewer.ActualWidth,
            scrollViewer.ActualHeight,
            scrollViewer.ViewportWidth,
            scrollViewer.ViewportHeight,
            panel.ActualWidth,
            panel.ActualHeight,
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset
        );
    }

    #region Mappers

    /// <summary>
    /// Maps the content property from the scroll box to the platform content panel.
    /// </summary>
    public static void MapContent(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._contentPanel is not { } panel)
        {
            return;
        }

        panel.Children.Clear();

        if (scrollBox.PresentedContent is { } content && handler.MauiContext is { } mauiContext)
        {
            panel.Children.Add(content.ToPlatform(mauiContext));
        }
    }

    /// <summary>
    /// Maps the orientation to the platform scroll viewer.
    /// </summary>
    public static void MapOrientation(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollViewer is not { } scrollViewer)
        {
            return;
        }

        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;

        scrollViewer.VerticalScrollMode = horizontal ? WScrollMode.Disabled : WScrollMode.Enabled;
        scrollViewer.HorizontalScrollMode = horizontal ? WScrollMode.Enabled : WScrollMode.Disabled;

        if (!handler.IsConnecting)
        {
            scrollViewer.ChangeView(0, 0, null, disableAnimation: true);
            (scrollBox as ScrollBox)?.UpdateScrollPosition(0, 0);
        }

        MapScrollBarVisibility(handler, scrollBox);
        MapIsScrollEnabled(handler, scrollBox);
    }

    /// <summary>
    /// Maps the scroll gestures enablement to the platform scroll viewer.
    /// </summary>
    public static void MapIsScrollEnabled(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollViewer is not { } scrollViewer)
        {
            return;
        }

        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;

        if (scrollBox.IsScrollEnabled)
        {
            scrollViewer.VerticalScrollMode = horizontal ? WScrollMode.Disabled : WScrollMode.Enabled;
            scrollViewer.HorizontalScrollMode = horizontal ? WScrollMode.Enabled : WScrollMode.Disabled;
        }
        else
        {
            scrollViewer.VerticalScrollMode = WScrollMode.Disabled;
            scrollViewer.HorizontalScrollMode = WScrollMode.Disabled;
        }
    }

    /// <summary>
    /// Maps the scroll bar visibility to the platform scroll viewer.
    /// </summary>
    public static void MapScrollBarVisibility(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollViewer is not { } scrollViewer)
        {
            return;
        }

        var visibility = scrollBox.ScrollBarVisibility switch
        {
            ScrollBarVisibility.Always => WScrollBarVisibility.Visible,
            ScrollBarVisibility.Never => WScrollBarVisibility.Hidden,
            _ => WScrollBarVisibility.Auto
        };

        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;
        scrollViewer.VerticalScrollBarVisibility = horizontal ? WScrollBarVisibility.Disabled : visibility;
        scrollViewer.HorizontalScrollBarVisibility = horizontal ? visibility : WScrollBarVisibility.Disabled;
    }

    /// <summary>
    /// Not supported on Windows: the fading edge is accepted but inactive.
    /// </summary>
    public static void MapFadingEdgeLength(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        // Not supported on Windows.
    }

    private partial void MeasurePlatformScroller(double widthConstraint, double heightConstraint)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            return;
        }

        scrollViewer.Measure(new global::Windows.Foundation.Size(
                double.IsNaN(widthConstraint) ? double.PositiveInfinity : widthConstraint,
                double.IsNaN(heightConstraint) ? double.PositiveInfinity : heightConstraint
            )
        );
    }

    private partial void UpdateFillViewport(IScrollBox scrollBox)
    {
        // The WinUI ScrollViewer content presenter already stretches short content to the
        // viewport; a hugging box is content-sized so no switch is needed.
    }

    /// <summary>
    /// Not supported on Windows: pull-to-refresh is accepted but inactive.
    /// </summary>
    public static void MapIsRefreshEnabled(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        // Not supported on Windows.
    }

    /// <summary>
    /// Not supported on Windows: pull-to-refresh is accepted but inactive.
    /// </summary>
    public static void MapIsRefreshing(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        // Not supported on Windows.
    }

    /// <summary>
    /// Not supported on Windows: pull-to-refresh is accepted but inactive.
    /// </summary>
    public static void MapRefreshAccentColor(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        // Not supported on Windows.
    }

    /// <summary>
    /// Maps the ScrollTo command to the platform scroll viewer.
    /// </summary>
    public static void MapScrollTo(ScrollBoxHandler handler, IScrollBox scrollBox, object? args)
    {
        if (args is not ScrollBoxScrollToRequest request)
        {
            return;
        }

        // A newer request supersedes queued or in-flight ones (their tasks still complete).
        handler._pendingScrollToRequest?.Complete();
        handler._pendingScrollToRequest = null;
        handler._activeScrollToRequest?.Complete();
        handler._activeScrollToRequest = null;

        if (handler._scrollViewer is not { } scrollViewer)
        {
            request.Complete();

            return;
        }

        if (!scrollViewer.IsLoaded || (scrollViewer.ActualWidth <= 0 && scrollViewer.ActualHeight <= 0))
        {
            // ChangeView is silently ignored until the ScrollViewer is loaded and laid out:
            // queue the request; the Loaded event (or the first content size change after it)
            // flushes the queue.
            handler._pendingScrollToRequest = request;

            return;
        }

        handler.ExecuteScrollToRequest(request);
    }

    private void ExecuteScrollToRequest(ScrollBoxScrollToRequest request)
    {
        if (_scrollViewer is not { } scrollViewer)
        {
            request.Complete();

            return;
        }

        // ScrollableWidth/Height lag the content's SizeChanged by a layout pass: without this,
        // a request arriving right after a content change clamps a legitimate target to 0.
        scrollViewer.UpdateLayout();

        var targetX = Math.Clamp(request.X, 0, scrollViewer.ScrollableWidth);
        var targetY = Math.Clamp(request.Y, 0, scrollViewer.ScrollableHeight);

        if (Math.Abs(scrollViewer.HorizontalOffset - targetX) < 0.5 && Math.Abs(scrollViewer.VerticalOffset - targetY) < 0.5)
        {
            request.Complete();

            return;
        }

        // ChangeView applies asynchronously even with disableAnimation; on a LOADED viewer it is
        // reliable, and completion comes from ViewChanged (IsIntermediate=false). The not-loaded
        // case never reaches this method: HandleScrollToRequest queues it and the Loaded event
        // flushes the queue.
        _activeScrollToRequest = request;

        if (!scrollViewer.ChangeView(targetX, targetY, null, disableAnimation: !request.Animated))
        {
            _activeScrollToRequest = null;
            request.Complete();
        }
    }

    /// <summary>
    /// Invalidates the measure of the scroll viewer AND of its content panel.
    /// </summary>
    /// <remarks>
    /// WinUI invalidation does not travel from a parent to its children: invalidating only the
    /// ScrollViewer (what the default mapper does) leaves the content panel holding the measure
    /// it computed for its previous content, so <c>CrossPlatformMeasure</c> never re-runs and a
    /// hugging <see cref="ScrollBox.SizingStrategy" /> can neither grow nor shrink. MAUI's own
    /// ScrollViewHandler overrides this command for the same reason.
    /// </remarks>
    public static void MapInvalidateMeasure(ScrollBoxHandler handler, IScrollBox scrollBox, object? args)
    {
        handler.PlatformView?.InvalidateMeasure(scrollBox);
        handler._contentPanel?.InvalidateMeasure();
    }

    /// <summary>
    /// Maps the scroll event enabled state to the platform scroll listener.
    /// </summary>
    public static void MapSetScrollEventEnabled(ScrollBoxHandler handler, IScrollBox scrollBox, object? args)
    {
        if (args is bool enabled)
        {
            handler._scrollEventsEnabled = enabled;
        }
    }

    #endregion
}
