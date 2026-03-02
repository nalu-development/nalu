using Microsoft.Maui.Handlers;
using ScrollView = Android.Widget.ScrollView;
#if __IOS__ || MACCATALYST
using UIKit;
using PlatformView = UIKit.UIScrollView;
#elif ANDROID
using System;
using Android.OS;
using Android.Content;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Core.Widget;
using Microsoft.Maui.Platform;
using View = Android.Views.View;
using PlatformView = Microsoft.Maui.Platform.MauiScrollView;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.Controls.ScrollViewer;
#elif TIZEN
using PlatformView = Tizen.UIExtensions.NUI.ScrollView;
#elif (NETSTANDARD || !PLATFORM) || (NET6_0_OR_GREATER && !IOS && !ANDROID && !TIZEN)
using PlatformView = System.Object;
#endif
namespace Nalu;

#if ANDROID
/// <summary>
/// Custom ScrollView that handles touch-up-after-drag, snap-to-nearest-page, and notifies via callback (matches iOS behavior).
/// </summary>
internal class NaluSlideScrollView : MauiScrollView
{
    private bool _hasDragged;
    private bool _isHorizontal;
    private Action? _onSnapComplete;
    private Timer? _snapEndTimer;

    public NaluSlideScrollView(Context context)
        : base(context)
    {
    }

    public NaluSlideScrollView(Context context, IAttributeSet? attrs)
        : base(context, attrs!)
    {
    }

    public NaluSlideScrollView(Context context, IAttributeSet? attrs, int defStyleAttr)
        : base(context, attrs!, defStyleAttr)
    {
    }

    /// <summary>Configures orientation and callback. Pass null callback to clear and stop timers.</summary>
    public void SetSnapConfig(bool isHorizontal, Action? onSnapComplete)
    {
        _isHorizontal = isHorizontal;
        _onSnapComplete = onSnapComplete;
        _snapEndTimer?.Dispose();
        _snapEndTimer = null;
    }

    public override bool OnInterceptTouchEvent(MotionEvent? ev)
    {
        if (ev != null)
        {
            TrackTouchForDragEnd(ev);
        }

        return base.OnInterceptTouchEvent(ev);
    }

    public override bool OnTouchEvent(MotionEvent? ev)
    {
        if (ev != null)
        {
            TrackTouchForDragEnd(ev);
        }

        return base.OnTouchEvent(ev);
    }

    private void TrackTouchForDragEnd(MotionEvent ev)
    {
        var action = ev.Action;

        switch (action)
        {
            case MotionEventActions.Down:
                _hasDragged = false;

                break;
            case MotionEventActions.Move:
                _hasDragged = true;

                break;
            case MotionEventActions.Up or MotionEventActions.Cancel when _hasDragged:
                SnapToNearestPage(GetChildAt(0)!);

                break;
        }
    }

    private void SnapToNearestPage(View scrollView)
    {
        var viewportW = scrollView.Width;
        var viewportH = scrollView.Height;
        if (viewportW <= 0 || viewportH <= 0)
        {
            _onSnapComplete?.Invoke();
            return;
        }

        // Use scroll position from the view that actually scrolls (target); NestedScrollView's own ScrollX/ScrollY may be zero if the scroller is a child.
        ComputeScroll();
        var currentScrollX = scrollView.ScrollX;
        var currentScrollY = scrollView.ScrollY;
        View? firstChild = null;
        if (scrollView is ViewGroup { ChildCount: > 0 } vg)
        {
            firstChild = vg.GetChildAt(0);
        }
        var contentWidth = firstChild?.Width ?? 0;
        var contentHeight = firstChild?.Height ?? 0;

        var targetX = currentScrollX;
        var targetY = currentScrollY;
        var didStartSnap = false;

        if (_isHorizontal)
        {
            var pageSize = viewportW;
            var maxScrollX = Math.Max(0, contentWidth - viewportW);
            var targetPage = (int)Math.Round((double)currentScrollX / pageSize, MidpointRounding.AwayFromZero);
            var targetOffset = Math.Clamp(targetPage * pageSize, 0, maxScrollX);
            if (Math.Abs(targetOffset - currentScrollX) > 1)
            {
                targetX = targetOffset;
                targetY = currentScrollY;
                didStartSnap = true;
            }
        }
        else
        {
            var pageSize = viewportH;
            var maxScrollY = Math.Max(0, contentHeight - viewportH);
            var targetPage = (int)Math.Round((double)currentScrollY / pageSize, MidpointRounding.AwayFromZero);
            var targetOffset = Math.Clamp(targetPage * pageSize, 0, maxScrollY);
            if (Math.Abs(targetOffset - currentScrollY) > 1)
            {
                targetX = currentScrollX;
                targetY = targetOffset;
                didStartSnap = true;
            }
        }

        if (!didStartSnap)
        {
            _onSnapComplete?.Invoke();
            return;
        }

        switch (scrollView)
        {
            case HorizontalScrollView horizontalScrollView:
                horizontalScrollView.Fling(0);
                break;
            case ScrollView nestedScrollView:
                nestedScrollView.Fling(0);
                break;
        }

        SmoothScrollTo(targetX, targetY);

        const int snapDurationMs = 300;
        var callback = _onSnapComplete;
        var viewToPost = scrollView;
        _snapEndTimer?.Dispose();
        _snapEndTimer = new Timer(_ =>
        {
            _snapEndTimer?.Dispose();
            _snapEndTimer = null;
            viewToPost.Post(() => callback?.Invoke());
        }, null, snapDurationMs, Timeout.Infinite);
    }
}
#endif

internal class SlideScrollViewHandler : ScrollViewHandler
{
    public new SlideScrollView VirtualView => (SlideScrollView)base.VirtualView;

#if ANDROID
    protected override MauiScrollView CreatePlatformView()
    {
        var context = Context ?? throw new InvalidOperationException("Context is required to create NaluSlideScrollView.");
        return new NaluSlideScrollView(context);
    }
#endif

    protected override void ConnectHandler(PlatformView platformView)
    {
        base.ConnectHandler(platformView);

#if IOS || MACCATALYST
        platformView.PagingEnabled = true;
        platformView.DraggingEnded += OnDraggingEnded;
        platformView.DecelerationEnded += OnDecelerationEnded;
#elif ANDROID
        if (platformView is NaluSlideScrollView naluScrollView)
        {
            var isHorizontal = VirtualView.Orientation == ScrollOrientation.Horizontal;
            naluScrollView.SetSnapConfig(isHorizontal, () => VirtualView.SendDraggingEnded());
        }
#endif
    }

#if IOS || MACCATALYST
    private void OnDecelerationEnded(object? sender, EventArgs e) => VirtualView.SendDraggingEnded();

    private void OnDraggingEnded(object? sender, DraggingEventArgs e)
    {
        if (!e.Decelerate)
        {
            VirtualView.SendDraggingEnded();
        }
    }
#endif

    protected override void DisconnectHandler(PlatformView platformView)
    {
#if IOS || MACCATALYST
        platformView.DraggingEnded -= OnDraggingEnded;
        platformView.DecelerationEnded -= OnDecelerationEnded;
#elif ANDROID
        if (platformView is NaluSlideScrollView naluScrollView)
        {
            naluScrollView.SetSnapConfig(false, null);
        }
#endif

        base.DisconnectHandler(platformView);
    }
}
