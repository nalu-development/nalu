using CoreFoundation;
using CoreGraphics;
using Microsoft.Maui.Platform;
using Nalu.Internals;
using UIKit;

namespace Nalu;

#pragma warning disable IDE0060
// ReSharper disable UnusedParameter.Local

/// <summary>
/// Handler for the <see cref="ScrollBox" /> view on iOS and Mac Catalyst.
/// </summary>
public partial class ScrollBoxHandler
{
    private ScrollBoxScrollView? _scrollView;
    private ScrollBoxContentView? _contentWrapper;
    private ScrollBoxContainerView? _containerView;
    private ScrollBoxScrollViewDelegate? _delegate;
    private UIRefreshControl? _refreshControl;
    private nfloat? _refreshRevealRestoreOffsetY;
    private bool _isUpdatingIsRefreshingFromPlatform;
    private bool _scrollSessionActive;
    private bool _hasLaidOutContent;
    private ScrollBoxScrollToRequest? _pendingScrollToRequest;
    private ScrollBoxScrollToRequest? _activeScrollToRequest;
    private int _scrollToGeneration;

    /// <inheritdoc />
    protected override UIView CreatePlatformView()
    {
        var scrollBox = VirtualView;

        var scrollView = new ScrollBoxScrollView
        {
            Orientation = scrollBox.Orientation,
            CrossPlatformLayout = scrollBox,
            ContentLaidOut = OnPlatformContentLaidOut
        };
        _scrollView = scrollView;

        var wrapper = new ScrollBoxContentView();
        _contentWrapper = wrapper;
        scrollView.ContentWrapper = wrapper;
        scrollView.AddSubview(wrapper);

        _delegate = new ScrollBoxScrollViewDelegate(this);
        _delegate.UpdateOrientation(scrollBox.Orientation);
        scrollView.Delegate = _delegate;

        ConfigureBounce(scrollView, scrollBox.Orientation);

        _refreshControl = new UIRefreshControl();
        _refreshControl.AddTarget(RefreshControlEventHandler, UIControlEvent.ValueChanged);

        // Wrap the scroll view in a container view: the fading-edge gradient mask must live on
        // the scroll view's superview layer.
        var containerView = new ScrollBoxContainerView();
        _containerView = containerView;
        containerView.BoundsChanged += OnContainerBoundsChanged;

        scrollView.TranslatesAutoresizingMaskIntoConstraints = false;
        containerView.AddSubview(scrollView);

        NSLayoutConstraint.ActivateConstraints([
            scrollView.TopAnchor.ConstraintEqualTo(containerView.TopAnchor),
            scrollView.LeadingAnchor.ConstraintEqualTo(containerView.LeadingAnchor),
            scrollView.TrailingAnchor.ConstraintEqualTo(containerView.TrailingAnchor),
            scrollView.BottomAnchor.ConstraintEqualTo(containerView.BottomAnchor)
        ]);

        return containerView;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(UIView platformView)
    {
        _pendingScrollToRequest?.Complete();
        _pendingScrollToRequest = null;
        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;

        if (_refreshControl is not null)
        {
            _refreshControl.RemoveTarget(RefreshControlEventHandler, UIControlEvent.ValueChanged);
            _refreshControl.RemoveFromSuperview();
            _refreshControl.Dispose();
            _refreshControl = null;
        }

        if (_containerView is not null)
        {
            _containerView.BoundsChanged -= OnContainerBoundsChanged;
        }

        if (_scrollView is not null)
        {
            _scrollView.ContentLaidOut = null;
            _scrollView.CrossPlatformLayout = null;
            _scrollView.ContentWrapper = null;
            _scrollView.Delegate = null!;
        }

        _delegate?.Dispose();
        _delegate = null;
        _contentWrapper?.Dispose();
        _contentWrapper = null;
        _scrollView?.Dispose();
        _scrollView = null;
        _containerView?.Dispose();
        _containerView = null;
        _hasLaidOutContent = false;

        base.DisconnectHandler(platformView);
    }

    private static void ConfigureBounce(ScrollBoxScrollView scrollView, ScrollBoxOrientation orientation)
    {
        scrollView.AlwaysBounceVertical = orientation == ScrollBoxOrientation.Vertical;
        scrollView.AlwaysBounceHorizontal = orientation == ScrollBoxOrientation.Horizontal;
    }

    private void OnContainerBoundsChanged(object? sender, EventArgs e)
    {
        if (_scrollView is { } scrollView)
        {
            _delegate?.UpdateFadingEdge(scrollView);
        }
    }

    #region Scroll geometry / events

    /// <summary>
    /// The distance scrolled from the start of the content on each axis. On the scroll axis the
    /// adjusted content inset defines the resting position; the cross axis is not scrollable.
    /// </summary>
    private (double ScrollX, double ScrollY) GetScrollDistances(ScrollBoxScrollView scrollView)
        => (
            Math.Max(0, scrollView.ContentOffset.X + scrollView.AdjustedContentInset.Left),
            Math.Max(0, scrollView.ContentOffset.Y + scrollView.AdjustedContentInset.Top)
        );

    private (double ScrollX, double ScrollY, double TotalWidth, double TotalHeight) GetScrollValues(ScrollBoxScrollView scrollView)
    {
        var (scrollX, scrollY) = GetScrollDistances(scrollView);
        var inset = scrollView.AdjustedContentInset;

        return (
            scrollX,
            scrollY,
            scrollView.ContentSize.Width + inset.Left + inset.Right,
            scrollView.ContentSize.Height + inset.Top + inset.Bottom
        );
    }

    internal ScrollBoxGeometry? GetGeometry()
    {
        if (_scrollView is not { } scrollView || _contentWrapper is not { } wrapper || !_hasLaidOutContent)
        {
            return null;
        }

        var bounds = scrollView.Bounds;
        var inset = scrollView.AdjustedContentInset;
        var (scrollX, scrollY) = GetScrollDistances(scrollView);

        return new ScrollBoxGeometry(
            bounds.Width,
            bounds.Height,
            Math.Max(0, bounds.Width - inset.Left - inset.Right),
            Math.Max(0, bounds.Height - inset.Top - inset.Bottom),
            wrapper.Frame.Width,
            wrapper.Frame.Height,
            scrollX,
            scrollY
        );
    }

    internal void OnPlatformScrolled()
    {
        if (_scrollView is not { } scrollView)
        {
            return;
        }

        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scrollView);
        (VirtualView as IScrollBoxController)?.Scrolled(scrollX, scrollY, totalWidth, totalHeight);
    }

    internal void OnPlatformScrollStarted()
    {
        if (_scrollView is not { } scrollView || _scrollSessionActive)
        {
            return;
        }

        _scrollSessionActive = true;
        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scrollView);
        (VirtualView as IScrollBoxController)?.ScrollStarted(scrollX, scrollY, totalWidth, totalHeight);
    }

#pragma warning disable VSTHRD100
    internal async void OnPlatformScrollEnded()
#pragma warning restore VSTHRD100
    {
        // Give UIKit time to settle positions before sending the event.
        await Task.Yield();

        if (_scrollView is not { } scrollView || !_scrollSessionActive)
        {
            return;
        }

        _scrollSessionActive = false;
        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scrollView);
        (VirtualView as IScrollBoxController)?.ScrollEnded(scrollX, scrollY, totalWidth, totalHeight);
    }

    internal void OnPlatformScrollAnimationEnded()
    {
        if (_scrollView is { } scrollView)
        {
            var (scrollX, scrollY) = GetScrollDistances(scrollView);
            (VirtualView as ScrollBox)?.UpdateScrollPosition(scrollX, scrollY);
        }

        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;
    }

    private void OnPlatformContentLaidOut(double contentWidth, double contentHeight)
    {
        _hasLaidOutContent = true;

        OnContentLaidOut(contentWidth, contentHeight);

        if (_scrollView is { } scrollView)
        {
            _delegate?.UpdateFadingEdge(scrollView);
        }

        if (_pendingScrollToRequest is { } pending)
        {
            _pendingScrollToRequest = null;
            ExecuteScrollToRequest(pending);
        }
    }

    #endregion

    #region Mappers

    /// <summary>
    /// Maps the content property from the scroll box to the platform content wrapper.
    /// </summary>
    public static void MapContent(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._contentWrapper is not { } wrapper)
        {
            return;
        }

        foreach (var subview in wrapper.Subviews)
        {
            subview.RemoveFromSuperview();
        }

        if (scrollBox.PresentedContent is { } content && handler.MauiContext is { } mauiContext)
        {
            wrapper.AddSubview(content.ToPlatform(mauiContext));
        }

        handler._scrollView?.InvalidateContentMeasure();
    }

    /// <summary>
    /// Maps the orientation to the platform scroll view. On iOS the same scroll view serves both
    /// axes, so a runtime change simply re-measures and resets to the content start.
    /// </summary>
    public static void MapOrientation(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollView is not { } scrollView)
        {
            return;
        }

        scrollView.Orientation = scrollBox.Orientation;
        handler._delegate?.UpdateOrientation(scrollBox.Orientation);
        ConfigureBounce(scrollView, scrollBox.Orientation);

        if (handler.IsConnecting)
        {
            return;
        }

        handler._scrollSessionActive = false;
        var inset = scrollView.AdjustedContentInset;
        scrollView.SetContentOffset(new CGPoint(-inset.Left, -inset.Top), false);
        (scrollBox as ScrollBox)?.UpdateScrollPosition(0, 0);
        scrollView.InvalidateContentMeasure();
        MapFadingEdgeLength(handler, scrollBox);
    }

    /// <summary>
    /// Maps the scroll gestures enablement to the platform scroll view.
    /// </summary>
    public static void MapIsScrollEnabled(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollView is { } scrollView)
        {
            // Disables gestures only: programmatic SetContentOffset still works.
            scrollView.ScrollEnabled = scrollBox.IsScrollEnabled;
        }
    }

    /// <summary>
    /// Maps the scroll bar visibility to the platform scroll view.
    /// </summary>
    public static void MapScrollBarVisibility(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollView is not { } scrollView)
        {
            return;
        }

        // iOS cannot keep indicators permanently visible: Always behaves as Default.
        var visible = scrollBox.ScrollBarVisibility != ScrollBarVisibility.Never;
        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;

        scrollView.ShowsHorizontalScrollIndicator = horizontal && visible;
        scrollView.ShowsVerticalScrollIndicator = !horizontal && visible;
    }

    /// <summary>
    /// Maps the fading edge length to the platform scroll view.
    /// </summary>
    public static void MapFadingEdgeLength(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scrollView is { } scrollView)
        {
            handler._delegate?.UpdateFadingEdgeLength(scrollView, scrollBox.FadingEdgeLength);
        }
    }

    private partial void UpdateFillViewport(IScrollBox scrollBox)
    {
        if (_scrollView is { } scrollView)
        {
            scrollView.FillViewportEnabled = scrollBox.SizingStrategy.Mode == ScrollBoxSizingMode.Fill;

            if (!IsConnecting)
            {
                scrollView.InvalidateContentMeasure();
            }
        }
    }

    #endregion

    #region Pull-to-refresh

    private void RefreshControlEventHandler(object? sender, EventArgs e)
    {
        var virtualView = VirtualView;

        // User pulled to refresh - sync platform state to IsRefreshing first
        if (virtualView is ScrollBox scrollBox && _refreshControl is not null)
        {
            _isUpdatingIsRefreshingFromPlatform = true;
            scrollBox.SetValueFromRenderer(ScrollBox.IsRefreshingProperty, _refreshControl.Refreshing);
            _isUpdatingIsRefreshingFromPlatform = false;
        }

        // Then call Refresh() which will fire RefreshCommand/OnRefresh
        (virtualView as IScrollBoxController)?.Refresh(() => { /* Completion handled by IsRefreshing property */ });
    }

    /// <summary>
    /// Maps the refresh accent color to the platform refresh control.
    /// </summary>
    public static void MapRefreshAccentColor(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (scrollBox.RefreshAccentColor is not null && handler._refreshControl is not null)
        {
            handler._refreshControl.TintColor = scrollBox.RefreshAccentColor.ToPlatform();
        }
    }

    /// <summary>
    /// Maps the pull-to-refresh enablement to the platform refresh control.
    /// </summary>
    public static void MapIsRefreshEnabled(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        var refreshControl = handler._refreshControl;
        var scrollView = handler._scrollView;

        if (refreshControl is null || scrollView is null)
        {
            return;
        }

        var isRefreshEnabled = scrollBox.IsRefreshEnabled;
        refreshControl.Enabled = isRefreshEnabled;

        if (isRefreshEnabled && !ReferenceEquals(scrollView.RefreshControl, refreshControl))
        {
            scrollView.RefreshControl = refreshControl;
        }
        else if (!isRefreshEnabled && ReferenceEquals(scrollView.RefreshControl, refreshControl))
        {
            scrollView.RefreshControl = null;
        }
    }

    /// <summary>
    /// Maps the refreshing state to the platform refresh control.
    /// </summary>
    public static void MapIsRefreshing(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._refreshControl is null || handler._scrollView is null || handler._isUpdatingIsRefreshingFromPlatform)
        {
            return;
        }

        var isRefreshing = scrollBox.IsRefreshing;
        var refreshControl = handler._refreshControl;
        var scrollView = handler._scrollView;

        if (isRefreshing && !refreshControl.Refreshing)
        {
            // BeginRefreshing alone does NOT show the spinner: per Apple's recipe for
            // programmatic refresh, the scroll view must be scrolled to expose the control.
            // Capture the geometry BEFORE BeginRefreshing (which expands the top inset).
            var topInset = scrollView.AdjustedContentInset.Top;
            var isAtTop = scrollView.ContentOffset.Y <= -topInset + 1;
            var controlHeight = refreshControl.Frame.Height > 0 ? refreshControl.Frame.Height : 60;

            refreshControl.BeginRefreshing();

            // Only auto-reveal when the content rests at the top: revealing from a scrolled
            // position would yank the content away from what the user is reading.
            if (isAtTop)
            {
                handler._refreshRevealRestoreOffsetY = scrollView.ContentOffset.Y;
                scrollView.SetContentOffset(new CGPoint(scrollView.ContentOffset.X, -(controlHeight + topInset)), true);
            }
        }
        else if (!isRefreshing && refreshControl.Refreshing)
        {
            refreshControl.EndRefreshing();

            // Restore the exact pre-reveal offset once EndRefreshing's own asynchronous
            // adjustments (inset removal + offset compensation) have settled — restoring
            // earlier races them and lands offset by the control height.
            if (handler._refreshRevealRestoreOffsetY is { } restoreOffsetY)
            {
                handler._refreshRevealRestoreOffsetY = null;
                handler.RestoreOffsetWhenSettled(restoreOffsetY, scrollView.ContentOffset, checksLeft: 25);
            }
        }

        // Sync platform state back to IsRefreshing (two-way binding)
        if (refreshControl.Refreshing != isRefreshing && scrollBox is ScrollBox scrollBoxElement)
        {
            handler._isUpdatingIsRefreshingFromPlatform = true;
            scrollBoxElement.SetValueFromRenderer(ScrollBox.IsRefreshingProperty, refreshControl.Refreshing);
            handler._isUpdatingIsRefreshingFromPlatform = false;
        }
    }

    private void RestoreOffsetWhenSettled(nfloat restoreOffsetY, CGPoint lastOffset, int checksLeft)
        => DispatchQueue.MainQueue.DispatchAfter(
            new DispatchTime(DispatchTime.Now, 80_000_000L /* 80ms in ns */),
            () =>
            {
                if (_scrollView is not { } scrollView)
                {
                    return;
                }

                var offset = scrollView.ContentOffset;

                if (Math.Abs(offset.Y - lastOffset.Y) >= 0.5 && checksLeft > 0)
                {
                    RestoreOffsetWhenSettled(restoreOffsetY, offset, checksLeft - 1);

                    return;
                }

                // Skip when the user (or the platform) already moved past the target.
                if (offset.Y < restoreOffsetY - 0.5)
                {
                    scrollView.SetContentOffset(new CGPoint(offset.X, restoreOffsetY), true);
                }
            }
        );

    #endregion

    #region ScrollTo

    /// <summary>
    /// Maps the ScrollTo command to the platform scroll view.
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

        if (handler._scrollView is null)
        {
            request.Complete();

            return;
        }

        if (!handler._hasLaidOutContent)
        {
            // Before the first layout pass there is nothing to scroll yet: queue the request,
            // executed right after the first content measure/arrange.
            handler._pendingScrollToRequest = request;

            return;
        }

        handler.ExecuteScrollToRequest(request);
    }

    private void ExecuteScrollToRequest(ScrollBoxScrollToRequest request)
    {
        if (_scrollView is not { } scrollView || VirtualView is not { } scrollBox)
        {
            request.Complete();

            return;
        }

        var inset = scrollView.AdjustedContentInset;
        var bounds = scrollView.Bounds;
        var contentSize = scrollView.ContentSize;
        var offset = scrollView.ContentOffset;
        CGPoint target;

        // Distance-from-content-start → raw offset, clamped against the ADJUSTED insets: this is
        // the clamp MAUI's ScrollView forgets, making its programmatic scrolls under-shoot by
        // exactly the safe-area inset.
        if (scrollBox.Orientation == ScrollBoxOrientation.Horizontal)
        {
            var maxDistance = Math.Max(0, contentSize.Width + inset.Left + inset.Right - bounds.Width);
            target = new CGPoint(Math.Clamp(request.X, 0, maxDistance) - inset.Left, offset.Y);
        }
        else
        {
            var maxDistance = Math.Max(0, contentSize.Height + inset.Top + inset.Bottom - bounds.Height);
            target = new CGPoint(offset.X, Math.Clamp(request.Y, 0, maxDistance) - inset.Top);
        }

        if (Math.Abs(target.X - offset.X) < 0.5 && Math.Abs(target.Y - offset.Y) < 0.5)
        {
            request.Complete();

            return;
        }

        if (request.Animated)
        {
            _activeScrollToRequest = request;
            var generation = ++_scrollToGeneration;
            scrollView.SetContentOffset(target, true);

            // Safety net: ScrollAnimationEnded is not delivered when the animation is interrupted
            // (or the view is detached mid-flight) — the completion contract must hold anyway.
            CompleteScrollToWhenSettled(request, generation, scrollView.ContentOffset, checksLeft: 25);
        }
        else
        {
            scrollView.SetContentOffset(target, false);
            var (scrollX, scrollY) = GetScrollDistances(scrollView);
            (VirtualView as ScrollBox)?.UpdateScrollPosition(scrollX, scrollY);
            request.Complete();
        }
    }

    private void CompleteScrollToWhenSettled(ScrollBoxScrollToRequest request, int generation, CGPoint lastOffset, int checksLeft)
        => DispatchQueue.MainQueue.DispatchAfter(
            new DispatchTime(DispatchTime.Now, 80_000_000L /* 80ms in ns */),
            () =>
            {
                if (generation != _scrollToGeneration || !ReferenceEquals(_activeScrollToRequest, request))
                {
                    // Completed by ScrollAnimationEnded, or superseded by a newer request.
                    return;
                }

                if (_scrollView is not { } scrollView)
                {
                    _activeScrollToRequest = null;
                    request.Complete();

                    return;
                }

                var offset = scrollView.ContentOffset;

                if ((Math.Abs(offset.X - lastOffset.X) >= 0.5 || Math.Abs(offset.Y - lastOffset.Y) >= 0.5) && checksLeft > 0)
                {
                    CompleteScrollToWhenSettled(request, generation, offset, checksLeft - 1);

                    return;
                }

                OnPlatformScrollAnimationEnded();
            }
        );

    /// <summary>
    /// Maps the scroll event enabled state to the platform delegate.
    /// </summary>
    public static void MapSetScrollEventEnabled(ScrollBoxHandler handler, IScrollBox scrollBox, object? args)
    {
        if (args is bool enabled)
        {
            handler._delegate?.SetScrollEventsEnabled(enabled);
        }
    }

    #endregion
}
