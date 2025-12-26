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
    private VirtualScrollRecyclerViewScrollListener? _scrollListener;
    private bool _isUpdatingIsRefreshingFromPlatform;
    private IVirtualScrollFlattenedAdapter? _flattenedAdapter;

    /// <inheritdoc />
    protected override AView CreatePlatformView()
    {
        var context = Context;

        var recyclerView = new VirtualScrollRecyclerView(context);
        _recyclerView = recyclerView;

        // Scroll listener will be set up when needed via MapSetScrollEventEnabled

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
        
        if (_recyclerView is not null && _scrollListener is not null)
        {
            _recyclerView.RemoveOnScrollListener(_scrollListener);
            _scrollListener = null;
        }
        
        _recyclerViewAdapter?.Dispose();
        _recyclerViewAdapter = null;
        _recyclerView?.Dispose();
        _recyclerView = null;
        _swipeRefreshLayout?.Dispose();
        _swipeRefreshLayout = null;
        _rootLayout?.Dispose();
        _rootLayout = null;

        base.DisconnectHandler(platformView);

        EnsureCreatedCellsCleanup();
    }

    internal VirtualScrollRecyclerView GetRecyclerView() => _recyclerView ?? throw new InvalidOperationException("RecyclerView has not been created.");

    /// <summary>
    /// Gets the range of currently visible items in the virtual scroll.
    /// </summary>
    /// <returns>A <see cref="VirtualScrollRange"/> containing the first and last visible item positions, or <c>null</c> if no items are visible.</returns>
    public VirtualScrollRange? GetVisibleItemsRange()
    {
        if (_recyclerView is null || _flattenedAdapter is null)
        {
            return null;
        }

        var layoutManager = _recyclerView.GetLayoutManager();
        if (layoutManager is not LinearLayoutManager linearLayoutManager)
        {
            return null;
        }

        var firstVisiblePosition = linearLayoutManager.FindFirstVisibleItemPosition();
        var lastVisiblePosition = linearLayoutManager.FindLastVisibleItemPosition();

        if (firstVisiblePosition < 0 || lastVisiblePosition < 0)
        {
            return null;
        }

        (int Section, int Item)? start = null;
        (int Section, int Item)? end = null;

        for (var i = firstVisiblePosition; i <= lastVisiblePosition; i++)
        {
            var position = GetPositionFromFlattenedIndex(i);
            if (!position.HasValue)
            {
                continue;
            }

            if (!start.HasValue)
            {
                start = position;
            }

            end = position;
        }

        if (!start.HasValue || !end.HasValue)
        {
            return null;
        }

        return new VirtualScrollRange(start.Value.Section, start.Value.Item, end.Value.Section, end.Value.Item);
    }

    private (int Section, int Item)? GetPositionFromFlattenedIndex(int flattenedIndex)
    {
        if (_flattenedAdapter is null || !_flattenedAdapter.TryGetPositionInfo(flattenedIndex, out var positionType, out var sectionIdx))
        {
            return null;
        }

        return positionType switch
        {
            VirtualScrollFlattenedPositionType.GlobalHeader => (VirtualScrollRange.GlobalHeaderSectionIndex, 0),
            VirtualScrollFlattenedPositionType.GlobalFooter => (VirtualScrollRange.GlobalFooterSectionIndex, 0),
            VirtualScrollFlattenedPositionType.SectionHeader => (sectionIdx, VirtualScrollRange.SectionHeaderItemIndex),
            VirtualScrollFlattenedPositionType.SectionFooter => (sectionIdx, VirtualScrollRange.SectionFooterItemIndex),
            VirtualScrollFlattenedPositionType.Item => _flattenedAdapter.TryGetSectionAndItemIndex(flattenedIndex, out var itemSectionIdx, out var itemIdx)
                ? (itemSectionIdx, itemIdx)
                : null,
            _ => null
        };
    }

    /// <summary>
    /// Maps the adapter property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapAdapter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Dispose existing notifier if any
        handler._notifier?.Dispose();
        handler._notifier = null;

        var recyclerView = handler.GetRecyclerView();

        if (virtualScroll.Adapter is { } adapter)
        {
            var mauiContext = handler.MauiContext ?? throw new InvalidOperationException("MauiContext cannot be null when mapping the Adapter.");
            var layoutInfo = virtualScroll as IVirtualScrollLayoutInfo ?? throw new InvalidOperationException("VirtualScroll must implement IVirtualScrollLayoutInfo.");
            var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
            var recyclerViewAdapter = new VirtualScrollRecyclerViewAdapter(mauiContext, recyclerView, virtualScroll, flattenedAdapter);
            recyclerView.SetAdapter(recyclerViewAdapter);
            handler._recyclerViewAdapter = recyclerViewAdapter;
            handler._flattenedAdapter = flattenedAdapter;
            
            // Create a new notifier instance every time the adapter changes to ensure a fresh subscription
            handler._notifier = new VirtualScrollPlatformFlattenedAdapterNotifier(recyclerViewAdapter, flattenedAdapter);
        }
        else
        {
            recyclerView.SetAdapter(new EmptyVirtualScrollRecyclerViewAdapter());
            handler._recyclerViewAdapter?.Dispose();
            handler._recyclerViewAdapter = null;
            handler._flattenedAdapter = null;
        }
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
        
        // Update fading edge when layout changes (orientation might have changed)
        handler.UpdateFadingEdge(virtualScroll);
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
    /// Maps the header template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._recyclerViewAdapter?.NotifyDataSetChanged();
        }
    }

    /// <summary>
    /// Maps the footer template property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
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
    /// Maps the fading edge length property from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapFadingEdgeLength(VirtualScrollHandler handler, IVirtualScroll virtualScroll) => handler.UpdateFadingEdge(virtualScroll);

    private void UpdateFadingEdge(IVirtualScroll virtualScroll)
    {
        var recyclerView = _recyclerView;
        // ReSharper disable once UseNullPropagation
        if (recyclerView is null)
        {
            return;
        }

        // Fading edge must be updated when the view is already part of the visual tree otherwise it won't appear
        recyclerView.Post(DoUpdateFadingEdge);

        return;

        void DoUpdateFadingEdge()
        {
            if (recyclerView.Handle == IntPtr.Zero)
            {
                // This callback is asynchronous - the recycler view might have been disposed in the meantime
                return;
            }
            
            if (virtualScroll.FadingEdgeLength <= 0)
            {
                recyclerView.HorizontalFadingEdgeEnabled = false;
                recyclerView.VerticalFadingEdgeEnabled = false;
                return;
            }

            var orientation = virtualScroll.ItemsLayout is LinearVirtualScrollLayout linearLayout
                ? linearLayout.Orientation
                : ItemsLayoutOrientation.Vertical;

            switch (orientation)
            {
                case ItemsLayoutOrientation.Horizontal:
                    recyclerView.HorizontalFadingEdgeEnabled = true;
                    recyclerView.VerticalFadingEdgeEnabled = false;
                    break;
                case ItemsLayoutOrientation.Vertical:
                    recyclerView.HorizontalFadingEdgeEnabled = false;
                    recyclerView.VerticalFadingEdgeEnabled = true;
                    break;
            }

            var fadingEdgePx = (int)recyclerView.Context!.ToPixels(virtualScroll.FadingEdgeLength);
            recyclerView.SetFadingEdgeLength(fadingEdgePx);
        }
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


    /// <summary>
    /// Maps the ScrollTo command from the virtual scroll to the platform recycler view.
    /// </summary>
    public static void MapScrollTo(VirtualScrollHandler handler, IVirtualScroll virtualScroll, object? args)
    {
        if (args is not VirtualScrollCommandScrollToArgs scrollToArgs || handler._flattenedAdapter is null)
        {
            return;
        }

        var sectionIndex = scrollToArgs.SectionIndex;
        var itemIndex = scrollToArgs.ItemIndex;
        var position = scrollToArgs.Position;
        var animated = scrollToArgs.Animated;

        if (sectionIndex < 0 || virtualScroll.Adapter is null)
        {
            return;
        }

        // Validate section index
        var sectionCount = virtualScroll.Adapter.GetSectionCount();
        if (sectionIndex >= sectionCount)
        {
            return;
        }

        // If itemIndex is -1, check if section headers are enabled
        if (itemIndex == -1)
        {
            var layoutInfo = virtualScroll as IVirtualScrollLayoutInfo;
            
            // If section headers are not enabled, scroll to first item instead
            if (layoutInfo?.HasSectionHeader != true)
            {
                var itemCount = virtualScroll.Adapter.GetItemCount(sectionIndex);
                if (itemCount == 0)
                {
                    return;
                }
                itemIndex = 0;
            }
        }
        else
        {
            if (itemIndex < 0)
            {
                return;
            }

            // Validate item index
            var itemCount = virtualScroll.Adapter.GetItemCount(sectionIndex);
            if (itemIndex >= itemCount)
            {
                return;
            }
        }

        // Calculate flattened index using the flattened adapter
        // GetFlattenedIndexForItem handles itemIndex == -1 to return section header index
        var flattenedIndex = handler._flattenedAdapter.GetFlattenedIndexForItem(sectionIndex, itemIndex);
        if (flattenedIndex < 0 || handler._recyclerView is null)
        {
            return;
        }

        // Use ScrollHelper to handle the scroll with proper positioning
        var scrollHelper = handler._recyclerView.ScrollHelper;
        if (animated)
        {
            scrollHelper.AnimateScrollToPosition(flattenedIndex, position);
        }
        else
        {
            scrollHelper.JumpScrollToPosition(flattenedIndex, position);
        }
    }

    /// <summary>
    /// Maps the scroll event enabled state from the virtual scroll to the platform recycler view scroll listener.
    /// </summary>
    public static void MapSetScrollEventEnabled(VirtualScrollHandler handler, IVirtualScroll virtualScroll, object? args)
    {
        if (args is not bool enabled)
        {
            return;
        }

        var recyclerView = handler._recyclerView;
        if (recyclerView is null)
        {
            return;
        }

        if (enabled)
        {
            // Enable scroll events - add listener if not already added
            if (handler._scrollListener is null)
            {
                handler._scrollListener = new VirtualScrollRecyclerViewScrollListener((rv, scrollX, scrollY, totalWidth, totalHeight) =>
                {
                    if (virtualScroll is IVirtualScrollController controller)
                    {
                        controller.Scrolled(scrollX, scrollY, totalWidth, totalHeight);
                    }
                });
                recyclerView.AddOnScrollListener(handler._scrollListener);
            }
        }
        else
        {
            // Disable scroll events - remove listener
            if (handler._scrollListener is not null)
            {
                recyclerView.RemoveOnScrollListener(handler._scrollListener);
                handler._scrollListener = null;
            }
        }
    }
}
