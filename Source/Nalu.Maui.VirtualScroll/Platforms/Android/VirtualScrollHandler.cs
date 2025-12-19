using Android.Widget;
using AndroidX.RecyclerView.Widget;
using AndroidX.SwipeRefreshLayout.Widget;
using AViewGroup = Android.Views.ViewGroup;
using AView = Android.Views.View;
using Microsoft.Maui.Platform;
using PlatformView = Android.Views.View;

namespace Nalu;

#pragma warning disable IDE0060
// ReSharper disable UnusedParameter.Local

/// <summary>
/// Handler for the <see cref="VirtualScroll" /> view on Android.
/// </summary>
public partial class VirtualScrollHandler
{
    private VirtualScrollRecyclerView? _recyclerView;
    private VirtualScrollRecyclerViewAdapter? _recyclerViewAdapter;
    private VirtualScrollPlatformFlattenedAdapterNotifier? _notifier;
    private SwipeRefreshLayout? _swipeRefreshLayout;
    private FrameLayout? _rootLayout;
    private bool _isUpdatingIsRefreshingFromPlatform;

    /// <inheritdoc />
    protected override AView CreatePlatformView()
    {
        var context = Context;

        var recyclerView = new VirtualScrollRecyclerView(context);
        
        _recyclerView = recyclerView;

        _swipeRefreshLayout = new SwipeRefreshLayout(context);
        _swipeRefreshLayout.AddView(
            recyclerView,
            new AViewGroup.LayoutParams(
                AViewGroup.LayoutParams.MatchParent,
                AViewGroup.LayoutParams.MatchParent
            )
        );

        _swipeRefreshLayout.SetOnRefreshListener(new SwipeRefreshListener(() =>
        {
            // User pulled to refresh - sync platform state to IsRefreshing first
            if (VirtualView is { } virtualScroll && _swipeRefreshLayout is not null)
            {
                _isUpdatingIsRefreshingFromPlatform = true;
                virtualScroll.IsRefreshing = _swipeRefreshLayout.Refreshing;
                _isUpdatingIsRefreshingFromPlatform = false;
            }
            
            // Then call Refresh() which will fire RefreshCommand/OnRefresh
            if (VirtualView is IVirtualScrollController controller)
            {
                controller.Refresh(() => { /* Completion handled by IsRefreshing property */ });
            }
        }));

        _rootLayout = new FrameLayout(context);
        _rootLayout.LayoutParameters = new AViewGroup.LayoutParams(
            AViewGroup.LayoutParams.MatchParent,
            AViewGroup.LayoutParams.MatchParent
        );

        _rootLayout.AddView(
            _swipeRefreshLayout,
            new FrameLayout.LayoutParams(
                AViewGroup.LayoutParams.MatchParent,
                AViewGroup.LayoutParams.MatchParent
            )
        );

        return _rootLayout;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        _notifier?.Dispose();
        _notifier = null;
        _recyclerViewAdapter?.Dispose();
        _recyclerViewAdapter = null;
        _recyclerView?.Dispose();
        _recyclerView = null;
        _swipeRefreshLayout?.Dispose();
        _swipeRefreshLayout = null;
        _rootLayout?.Dispose();
        _rootLayout = null;
        base.DisconnectHandler(platformView);
    }

    protected VirtualScrollRecyclerView GetRecyclerView() => _recyclerView ?? throw new InvalidOperationException("RecyclerView has not been created.");

    /// <summary>
    /// Maps the adapter property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapAdapter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Dispose existing notifier if any
        handler._notifier?.Dispose();
        handler._notifier = null;

        var mauiContext = handler.MauiContext ?? throw new InvalidOperationException("MauiContext cannot be null when mapping the Adapter.");
        var virtualScrollAdapter = virtualScroll.Adapter ?? throw new InvalidOperationException("VirtualScroll must have an Adapter set before it can be displayed.");
        var layoutInfo = virtualScroll as IVirtualScrollLayoutInfo ?? throw new InvalidOperationException("VirtualScroll must implement IVirtualScrollLayoutInfo.");
        var flattenedAdapter = new VirtualScrollFlattenedAdapter(virtualScrollAdapter, layoutInfo);
        var recyclerViewAdapter = new VirtualScrollRecyclerViewAdapter(mauiContext, virtualScroll, flattenedAdapter);
        handler.GetRecyclerView().SetAdapter(recyclerViewAdapter);
        handler._recyclerViewAdapter = recyclerViewAdapter;
        
        // Create a new notifier instance every time the adapter changes to ensure a fresh subscription
        handler._notifier = new VirtualScrollPlatformFlattenedAdapterNotifier(recyclerViewAdapter, flattenedAdapter);
    }

    /// <summary>
    /// Maps the layout property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapLayout(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        var recyclerView = handler.GetRecyclerView();

        switch (virtualScroll.ItemsLayout)
        {
            case LinearVirtualScrollLayout linearLayout:
                var orientation = linearLayout.Orientation == ItemsLayoutOrientation.Vertical ? LinearLayoutManager.Vertical : LinearLayoutManager.Horizontal;
                var layoutManager = new LinearLayoutManager(recyclerView.Context)
                                    {
                                        Orientation = orientation
                                    };

                recyclerView.Orientation = linearLayout.Orientation;
                recyclerView.SetLayoutManager(layoutManager);

                break;
            default:
                throw new NotSupportedException($"Layout type {virtualScroll.ItemsLayout.GetType().Name} is not supported.");
        }

        recyclerView.LayoutParameters = new FrameLayout.LayoutParams(
            AViewGroup.LayoutParams.MatchParent,
            AViewGroup.LayoutParams.MatchParent
        );
    }

    /// <summary>
    /// Maps the item template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapItemTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._recyclerViewAdapter?.NotifyDataSetChanged();
        }
    }

    /// <summary>
    /// Maps the section header template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapSectionHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._recyclerViewAdapter?.NotifyDataSetChanged();
        }
    }

    /// <summary>
    /// Maps the section footer template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapSectionFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._recyclerViewAdapter?.NotifyDataSetChanged();
        }
    }

    /// <summary>
    /// Maps the header property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapHeader(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._recyclerViewAdapter?.NotifyDataSetChanged();
        }
    }

    /// <summary>
    /// Maps the footer property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapFooter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._recyclerViewAdapter?.NotifyDataSetChanged();
        }
    }

    /// <summary>
    /// Maps the refresh accent color property from the virtual scroll to the platform swipe refresh layout.
    /// </summary>
    public static void MapRefreshAccentColor(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (virtualScroll.RefreshAccentColor is not null && handler._swipeRefreshLayout is not null)
        {
            handler._swipeRefreshLayout.SetColorSchemeColors(virtualScroll.RefreshAccentColor.ToPlatform());
        }
    }

    /// <summary>
    /// Maps the is refresh enabled property from the virtual scroll to the platform swipe refresh layout.
    /// </summary>
    public static void MapIsRefreshEnabled(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler._swipeRefreshLayout is not null)
        {
            handler._swipeRefreshLayout.Enabled = virtualScroll.IsRefreshEnabled;
        }
    }

    /// <summary>
    /// Maps the is refreshing property from the virtual scroll to the platform swipe refresh layout.
    /// </summary>
    public static void MapIsRefreshing(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler._swipeRefreshLayout is null || handler._isUpdatingIsRefreshingFromPlatform)
        {
            return;
        }

        var isRefreshing = virtualScroll.IsRefreshing;
        handler._swipeRefreshLayout.Refreshing = isRefreshing;
    }

    /// <summary>
    /// Listener for swipe refresh events.
    /// </summary>
    private class SwipeRefreshListener : Java.Lang.Object, SwipeRefreshLayout.IOnRefreshListener
    {
        private readonly Action _onRefresh;

        public SwipeRefreshListener(Action onRefresh)
        {
            _onRefresh = onRefresh;
        }

        public void OnRefresh() => _onRefresh();
    }
}
