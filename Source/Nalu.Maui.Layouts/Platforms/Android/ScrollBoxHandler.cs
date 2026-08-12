using Android.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using Microsoft.Maui.Platform;
using Nalu.Internals;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using PlatformView = Android.Views.View;

namespace Nalu;

#pragma warning disable IDE0060
// ReSharper disable UnusedParameter.Local
/// <summary>
/// Handler for the <see cref="ScrollBox" /> view on Android.
/// </summary>
public partial class ScrollBoxHandler
{
    /// <summary>
    /// Quiet time on the scroll position after which scrolling counts as settled. Neither
    /// NestedScrollView nor HorizontalScrollView expose scroll-state callbacks, so idleness is
    /// observed rather than notified.
    /// </summary>
    private const int _idleTimeoutMilliseconds = 100;

    private FrameLayout? _rootLayout;
    private SwipeRefreshLayout? _swipeRefreshLayout;
    private IScrollBoxScroller? _scroller;
    private ContentViewGroup? _contentWrapper;
    private bool _isUpdatingIsRefreshingFromPlatform;
    private bool _scrollEventsEnabled;
    private bool _scrollSessionActive;
    private int _scrollGeneration;
    private int _lastSeenScrollGeneration;
    private bool _idleCheckScheduled;
    private ScrollBoxScrollToRequest? _pendingScrollToRequest;
    private ScrollBoxScrollToRequest? _activeScrollToRequest;

    /// <summary>Pre-bound so the pending idle check can be REMOVED from the view's queue.</summary>
    private Java.Lang.IRunnable? _idleCheckRunnable;

    /// <inheritdoc />
    protected override AView CreatePlatformView()
    {
        var context = Context;

        _contentWrapper = new ContentViewGroup(context)
        {
            CrossPlatformLayout = VirtualView
        };
        _contentWrapper.SetClipChildren(false);

        // Content-size feedback must observe the CONTENT, not the scroller: Android skips
        // onLayout for a child whose bounds did not change, and the scroller's bounds stay put
        // while its content grows or shrinks (the Apple/WinUI equivalents are LayoutSubviews and
        // the panel's SizeChanged).
        _contentWrapper.LayoutChange += OnContentWrapperLayoutChange;

        _scroller = CreateScroller(VirtualView.Orientation);
        AttachScroller(_scroller);

        _swipeRefreshLayout = new SwipeRefreshLayout(context);
        _swipeRefreshLayout.AddView(
            _scroller.View,
            new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent)
        );

        _swipeRefreshLayout.SetOnRefreshListener(new SwipeRefreshListener(() =>
                {
                    // User pulled to refresh - sync platform state to IsRefreshing first
                    if (VirtualView is ScrollBox scrollBox && _swipeRefreshLayout is not null)
                    {
                        _isUpdatingIsRefreshingFromPlatform = true;
                        scrollBox.SetValueFromRenderer(ScrollBox.IsRefreshingProperty, _swipeRefreshLayout.Refreshing);
                        _isUpdatingIsRefreshingFromPlatform = false;
                    }

                    // Then call Refresh() which will fire RefreshCommand/OnRefresh
                    (VirtualView as IScrollBoxController)?.Refresh(() => { /* Completion handled by IsRefreshing property */ });
                }
            )
        );

        _rootLayout = new FrameLayout(context);
        _rootLayout.LayoutParameters = new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent);

        _rootLayout.AddView(
            _swipeRefreshLayout,
            new FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent)
        );

        return _rootLayout;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        _pendingScrollToRequest?.Complete();
        _pendingScrollToRequest = null;
        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;

        // Detach the whole tree BEFORE disposing: a disposed managed peer that is still attached
        // keeps receiving native callbacks (onLayout, insets dispatch) and Java.Interop then has
        // to resurrect it from the native handle.
        if (_contentWrapper is not null)
        {
            _contentWrapper.LayoutChange -= OnContentWrapperLayoutChange;
            _contentWrapper.RemoveAllViews();
            _contentWrapper.CrossPlatformLayout = null;
        }

        if (_scroller is { } scroller)
        {
            scroller.LayoutCallback = null;
            scroller.ScrollChangedCallback = null;
            scroller.View.RemoveCallbacks(_idleCheckRunnable);

            if (_contentWrapper is not null)
            {
                scroller.ViewGroup.RemoveView(_contentWrapper);
            }

            _swipeRefreshLayout?.RemoveView(scroller.View);
            scroller.View.Dispose();
            _scroller = null;
        }

        _contentWrapper?.Dispose();
        _contentWrapper = null;

        // Handlers can be reused: leave no state from the previous connection behind.
        _idleCheckScheduled = false;
        _scrollSessionActive = false;
        _scrollEventsEnabled = false;
        _scrollGeneration = 0;
        _lastSeenScrollGeneration = 0;
        _swipeRefreshLayout?.Dispose();
        _swipeRefreshLayout = null;
        _rootLayout?.Dispose();
        _rootLayout = null;

        base.DisconnectHandler(platformView);
    }

    private IScrollBoxScroller CreateScroller(ScrollBoxOrientation orientation)
    {
        IScrollBoxScroller scroller = orientation == ScrollBoxOrientation.Horizontal
            ? new NaluHorizontalScrollView(Context)
            : new NaluNestedScrollView(Context);

        // Content-size feedback comes from the wrapper's LayoutChange (see CreatePlatformView):
        // routing it from here too would run the whole feedback pass twice per layout.
        scroller.ScrollChangedCallback = OnNativeScrollChanged;

        return scroller;
    }

    /// <summary>Adds the content wrapper to the scroller with orientation-appropriate layout params.</summary>
    private void AttachScroller(IScrollBoxScroller scroller)
    {
        if (_contentWrapper is not { } wrapper)
        {
            return;
        }

        var horizontal = scroller is NaluHorizontalScrollView;

        scroller.ViewGroup.AddView(
            wrapper,
            new FrameLayout.LayoutParams(
                horizontal ? AViewGroup.LayoutParams.WrapContent : AViewGroup.LayoutParams.MatchParent,
                horizontal ? AViewGroup.LayoutParams.MatchParent : AViewGroup.LayoutParams.WrapContent
            )
        );
    }

    #region Scroll events / idle detection

    private void OnNativeScrollChanged()
    {
        if (_scroller is not { } scroller || Context is null)
        {
            return;
        }

        _scrollGeneration++;

        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scroller);
        var controller = VirtualView as IScrollBoxController;

        // A user "scroll session" spans from the first movement under the finger to idleness
        // (including the fling after the finger lifts). Programmatic scrolls never start one.
        if (!_scrollSessionActive && scroller.IsUserInteracting)
        {
            _scrollSessionActive = true;
            controller?.ScrollStarted(scrollX, scrollY, totalWidth, totalHeight);
        }

        if (_scrollEventsEnabled)
        {
            controller?.Scrolled(scrollX, scrollY, totalWidth, totalHeight);
        }

        ScheduleIdleCheck();
    }

    private void ScheduleIdleCheck()
    {
        if (_idleCheckScheduled || _scroller is null)
        {
            return;
        }

        _idleCheckScheduled = true;
        _lastSeenScrollGeneration = _scrollGeneration;
        _idleCheckRunnable ??= new Java.Lang.Runnable(OnIdleCheck);
        _scroller.View.PostDelayed(_idleCheckRunnable, _idleTimeoutMilliseconds);
    }

    private void OnIdleCheck()
    {
        _idleCheckScheduled = false;

        if (_scroller is not { } scroller || Context is null)
        {
            return;
        }

        // Still moving, or the finger is resting mid-drag: not idle yet.
        if (_scrollGeneration != _lastSeenScrollGeneration || scroller.IsUserInteracting)
        {
            ScheduleIdleCheck();

            return;
        }

        var (scrollX, scrollY, totalWidth, totalHeight) = GetScrollValues(scroller);

        if (_scrollSessionActive)
        {
            _scrollSessionActive = false;
            (VirtualView as IScrollBoxController)?.ScrollEnded(scrollX, scrollY, totalWidth, totalHeight);
        }
        else
        {
            // Programmatic scrolls fire no user events but must still refresh ScrollX/ScrollY.
            (VirtualView as ScrollBox)?.UpdateScrollPosition(scrollX, scrollY);
        }

        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;
    }

    private (double ScrollX, double ScrollY, double TotalWidth, double TotalHeight) GetScrollValues(IScrollBoxScroller scroller)
    {
        var context = Context;
        var view = scroller.View;
        var wrapper = _contentWrapper;
        var contentWidth = wrapper?.Width ?? 0;
        var contentHeight = wrapper?.Height ?? 0;

        return (
            context.FromPixels(view.ScrollX),
            context.FromPixels(view.ScrollY),
            context.FromPixels(contentWidth + view.PaddingLeft + view.PaddingRight),
            context.FromPixels(contentHeight + view.PaddingTop + view.PaddingBottom)
        );
    }

    #endregion

    #region Layout feedback

    private void OnContentWrapperLayoutChange(object? sender, AView.LayoutChangeEventArgs e) => OnScrollerLaidOut();

    private void OnScrollerLaidOut()
    {
        if (_contentWrapper is not { } wrapper || Context is not { } context)
        {
            return;
        }

        OnContentLaidOut(context.FromPixels(wrapper.Width), context.FromPixels(wrapper.Height));

        if (_pendingScrollToRequest is { } pending && wrapper.Width + wrapper.Height > 0)
        {
            _pendingScrollToRequest = null;
            ExecuteScrollToRequest(pending);
        }
    }

    internal ScrollBoxGeometry? GetGeometry()
    {
        if (_scroller is not { } scroller || _contentWrapper is not { } wrapper || Context is not { } context)
        {
            return null;
        }

        var view = scroller.View;

        return new ScrollBoxGeometry(
            context.FromPixels(view.Width),
            context.FromPixels(view.Height),
            context.FromPixels(view.Width - view.PaddingLeft - view.PaddingRight),
            context.FromPixels(view.Height - view.PaddingTop - view.PaddingBottom),
            context.FromPixels(wrapper.Width),
            context.FromPixels(wrapper.Height),
            context.FromPixels(view.ScrollX),
            context.FromPixels(view.ScrollY)
        );
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

        wrapper.RemoveAllViews();

        if (scrollBox.PresentedContent is { } content && handler.MauiContext is { } mauiContext)
        {
            wrapper.AddView(content.ToPlatform(mauiContext));
        }
    }

    /// <summary>
    /// Maps the orientation by swapping the platform scroller inside the stable root, so the
    /// handler's platform view (and its attachment to the MAUI parent) never changes.
    /// </summary>
    public static void MapOrientation(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler.IsConnecting)
        {
            // CreatePlatformView already picked the right scroller.
            return;
        }

        if (handler._scroller is not { } oldScroller || handler._swipeRefreshLayout is not { } swipeRefreshLayout || handler._contentWrapper is not { } wrapper)
        {
            return;
        }

        // The old scroller carries the in-flight state: its queued idle check would never run
        // (its view is about to be disposed), leaving _idleCheckScheduled stuck true — after
        // which no ScrollEnded ever fires again and animated requests never complete.
        handler._pendingScrollToRequest?.Complete();
        handler._pendingScrollToRequest = null;
        handler._activeScrollToRequest?.Complete();
        handler._activeScrollToRequest = null;
        handler._idleCheckScheduled = false;
        handler._scrollGeneration = 0;
        handler._lastSeenScrollGeneration = 0;

        oldScroller.LayoutCallback = null;
        oldScroller.ScrollChangedCallback = null;

        if (handler._idleCheckRunnable is { } idleCheckRunnable)
        {
            oldScroller.View.RemoveCallbacks(idleCheckRunnable);
        }

        oldScroller.ViewGroup.RemoveView(wrapper);
        swipeRefreshLayout.RemoveView(oldScroller.View);

        var scroller = handler.CreateScroller(scrollBox.Orientation);
        handler._scroller = scroller;
        handler.AttachScroller(scroller);

        swipeRefreshLayout.AddView(
            scroller.View,
            new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent)
        );

        oldScroller.View.Dispose();

        // Offsets do not translate across axes: a swap starts at the content start.
        handler._scrollSessionActive = false;
        (scrollBox as ScrollBox)?.UpdateScrollPosition(0, 0);

        // Re-apply the scroller-scoped properties on the fresh instance.
        MapIsScrollEnabled(handler, scrollBox);
        MapScrollBarVisibility(handler, scrollBox);
        MapFadingEdgeLength(handler, scrollBox);
        handler.UpdateFillViewport(scrollBox);
    }

    /// <summary>
    /// Maps the scroll gestures enablement to the platform scroller.
    /// </summary>
    public static void MapIsScrollEnabled(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scroller is { } scroller)
        {
            scroller.ScrollGesturesEnabled = scrollBox.IsScrollEnabled;
        }
    }

    /// <summary>
    /// Maps the scroll bar visibility to the platform scroller.
    /// </summary>
    public static void MapScrollBarVisibility(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._scroller is not { } scroller)
        {
            return;
        }

        var view = scroller.View;
        var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;
        var enabled = scrollBox.ScrollBarVisibility != ScrollBarVisibility.Never;

        view.HorizontalScrollBarEnabled = horizontal && enabled;
        view.VerticalScrollBarEnabled = !horizontal && enabled;
        view.ScrollbarFadingEnabled = scrollBox.ScrollBarVisibility != ScrollBarVisibility.Always;
    }

    /// <summary>
    /// Maps the fading edge length to the platform scroller.
    /// </summary>
    public static void MapFadingEdgeLength(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        var scroller = handler._scroller;

        if (scroller is null)
        {
            return;
        }

        // Fading edge must be updated when the view is already part of the visual tree otherwise it won't appear
        scroller.View.Post(DoUpdateFadingEdge);

        return;

        void DoUpdateFadingEdge()
        {
            var view = scroller.View;

            if (view.Handle == IntPtr.Zero || !ReferenceEquals(handler._scroller, scroller))
            {
                // This callback is asynchronous - the scroller might have been disposed or swapped in the meantime
                return;
            }

            if (scrollBox.FadingEdgeLength <= 0)
            {
                view.HorizontalFadingEdgeEnabled = false;
                view.VerticalFadingEdgeEnabled = false;
                view.Invalidate();

                return;
            }

            var horizontal = scrollBox.Orientation == ScrollBoxOrientation.Horizontal;
            view.HorizontalFadingEdgeEnabled = horizontal;
            view.VerticalFadingEdgeEnabled = !horizontal;
            view.SetFadingEdgeLength((int) view.Context!.ToPixels(scrollBox.FadingEdgeLength));

            // setXxxFadingEdgeEnabled / setFadingEdgeLength only flip view flags — they do NOT
            // invalidate, so under hardware rendering the cached display list keeps drawing
            // without the fade until the next unrelated redraw. Force it.
            view.Invalidate();
        }
    }

    private partial void MeasurePlatformScroller(double widthConstraint, double heightConstraint)
    {
        // The scroller measures its content from its own layout callback.
    }

    private partial void UpdateFillViewport(IScrollBox scrollBox)
    {
        var fill = scrollBox.SizingStrategy.Mode == ScrollBoxSizingMode.Fill;

        switch (_scroller)
        {
            case NaluNestedScrollView nestedScrollView:
                nestedScrollView.FillViewport = fill;

                break;
            case NaluHorizontalScrollView horizontalScrollView:
                horizontalScrollView.FillViewport = fill;

                break;
        }
    }

    /// <summary>
    /// Maps the refresh accent color to the platform swipe refresh layout.
    /// </summary>
    public static void MapRefreshAccentColor(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (scrollBox.RefreshAccentColor is not null && handler._swipeRefreshLayout is not null)
        {
            handler._swipeRefreshLayout.SetColorSchemeColors(scrollBox.RefreshAccentColor.ToPlatform());
        }
    }

    /// <summary>
    /// Maps the pull-to-refresh enablement to the platform swipe refresh layout.
    /// </summary>
    public static void MapIsRefreshEnabled(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._swipeRefreshLayout is not null)
        {
            handler._swipeRefreshLayout.Enabled = scrollBox.IsRefreshEnabled;
        }
    }

    /// <summary>
    /// Maps the refreshing state to the platform swipe refresh layout.
    /// </summary>
    public static void MapIsRefreshing(ScrollBoxHandler handler, IScrollBox scrollBox)
    {
        if (handler._swipeRefreshLayout is null || handler._isUpdatingIsRefreshingFromPlatform)
        {
            return;
        }

        handler._swipeRefreshLayout.Refreshing = scrollBox.IsRefreshing;
    }

    /// <summary>
    /// Maps the ScrollTo command to the platform scroller.
    /// </summary>
    public static void MapScrollTo(ScrollBoxHandler handler, IScrollBox scrollBox, object? args)
    {
        if (args is ScrollBoxScrollToRequest request)
        {
            handler.HandleScrollToRequest(request);
        }
    }

    private void HandleScrollToRequest(ScrollBoxScrollToRequest request)
    {
        // A newer request supersedes queued or in-flight ones (their tasks still complete).
        _pendingScrollToRequest?.Complete();
        _pendingScrollToRequest = null;
        _activeScrollToRequest?.Complete();
        _activeScrollToRequest = null;

        if (_scroller is null || _contentWrapper is not { } wrapper)
        {
            request.Complete();

            return;
        }

        if (wrapper.Width + wrapper.Height == 0)
        {
            // Before the first layout pass there is nothing to scroll yet: queue the request,
            // executed from the scroller's first layout callback.
            _pendingScrollToRequest = request;

            return;
        }

        ExecuteScrollToRequest(request);
    }

    private void ExecuteScrollToRequest(ScrollBoxScrollToRequest request)
    {
        if (_scroller is not { } scroller || _contentWrapper is not { } wrapper || Context is not { } context)
        {
            request.Complete();

            return;
        }

        var view = scroller.View;
        var horizontal = scroller is NaluHorizontalScrollView;
        var targetX = 0;
        var targetY = 0;

        if (horizontal)
        {
            var maxScrollX = Math.Max(0, wrapper.Width + view.PaddingLeft + view.PaddingRight - view.Width);
            targetX = Math.Clamp((int) context.ToPixels(request.X), 0, maxScrollX);
        }
        else
        {
            var maxScrollY = Math.Max(0, wrapper.Height + view.PaddingTop + view.PaddingBottom - view.Height);
            targetY = Math.Clamp((int) context.ToPixels(request.Y), 0, maxScrollY);
        }

        if (Math.Abs(view.ScrollX - targetX) <= 1 && Math.Abs(view.ScrollY - targetY) <= 1)
        {
            request.Complete();

            return;
        }

        if (request.Animated)
        {
            _activeScrollToRequest = request;
            scroller.SmoothScrollToPx(targetX, targetY);
            ScheduleIdleCheck();
        }
        else
        {
            scroller.StopAndJumpToPx(targetX, targetY);
            (VirtualView as ScrollBox)?.UpdateScrollPosition(context.FromPixels(view.ScrollX), context.FromPixels(view.ScrollY));
            request.Complete();
        }
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

    /// <summary>
    /// Listener for swipe refresh events.
    /// </summary>
    private class SwipeRefreshListener(Action onRefresh) : Java.Lang.Object, SwipeRefreshLayout.IOnRefreshListener
    {
        public void OnRefresh() => onRefresh();
    }
}
