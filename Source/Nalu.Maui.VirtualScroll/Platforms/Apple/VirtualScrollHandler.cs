using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

#pragma warning disable IDE0060
// ReSharper disable UnusedParameter.Local

/// <summary>
/// Handler for the <see cref="VirtualScroll" /> view on iOS and Mac Catalyst.
/// </summary>
public partial class VirtualScrollHandler
{
    private VirtualScrollPlatformReuseIdManager? _reuseIdManager;
    private VirtualScrollPlatformDataSourceNotifier? _notifier;
    private UIRefreshControl? _refreshControl;
    private bool _isUpdatingIsRefreshingFromPlatform;

    /// <summary>
    /// Gets the <see cref="UICollectionView"/> platform view.
    /// </summary>
    /// <exception cref="InvalidOperationException">when the handler is not connected.</exception>
    protected UICollectionView CollectionView => (UICollectionView)PlatformView;

    /// <inheritdoc />
    protected override UIView CreatePlatformView()
    {
        var layout = CreatePlatformLayout(this, VirtualView);
        var collectionView = new VirtualScrollCollectionView(CGRect.Empty, layout);
        var reuseIdManager = new VirtualScrollPlatformReuseIdManager(collectionView);

        _reuseIdManager = reuseIdManager;

        // Create refresh control
        _refreshControl = new UIRefreshControl();
        _refreshControl.Enabled = VirtualView.IsRefreshEnabled;
        _refreshControl.AddTarget((s, a) =>
        {
            // User pulled to refresh - sync platform state to IsRefreshing first
            if (VirtualView is VirtualScroll virtualScroll && _refreshControl is not null)
            {
                _isUpdatingIsRefreshingFromPlatform = true;
                virtualScroll.IsRefreshing = _refreshControl.Refreshing;
                _isUpdatingIsRefreshingFromPlatform = false;
            }
            
            // Then call Refresh() which will fire RefreshCommand/OnRefresh
            if (VirtualView is IVirtualScrollController controller)
            {
                controller.Refresh(() => { /* Completion handled by IsRefreshing property */ });
            }
        }, UIControlEvent.ValueChanged);

        collectionView.AlwaysBounceVertical = true;
        collectionView.RefreshControl = _refreshControl;

        return collectionView;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(UIView platformView)
    {
        _notifier?.Dispose();
        _notifier = null;
        
        if (_refreshControl is not null)
        {
            _refreshControl.RemoveFromSuperview();
            _refreshControl.Dispose();
            _refreshControl = null;
        }
        
        base.DisconnectHandler(platformView);
        var collectionView = (VirtualScrollCollectionView)platformView;
        collectionView.DataSource = null!;
        collectionView.RefreshControl = null;
        platformView.Dispose();
        _reuseIdManager = null;
    }

    /// <summary>
    /// Maps the adapter property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapAdapter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Dispose existing notifier if any
        handler._notifier?.Dispose();
        handler._notifier = null;

        var collectionView = handler.CollectionView;

        if (virtualScroll.Adapter is { } adapter)
        {
            var reuseIdManager = handler._reuseIdManager ?? throw new InvalidOperationException("ReuseIdManager is not initialized.");
            collectionView.DataSource = new VirtualScrollPlatformDataSource(adapter, virtualScroll, reuseIdManager);
            
            // Create a new notifier instance every time the adapter changes to ensure a fresh subscription
            handler._notifier = new VirtualScrollPlatformDataSourceNotifier(collectionView, adapter);
        }
        else
        {
            collectionView.DataSource = new EmptyVirtualScrollPlatformDataSource();
        }
    }

    /// <summary>
    /// Maps the layout property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapLayout(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler.IsConnecting)
        {
            return;
        }

        var layout = CreatePlatformLayout(handler, virtualScroll);
        var collectionView = handler.CollectionView;
        var animated = collectionView.Window is not null;
        collectionView.SetCollectionViewLayout(layout, animated);
    }
    
    private static UICollectionViewLayout CreatePlatformLayout(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        var layoutInfo = virtualScroll as IVirtualScrollLayoutInfo ?? throw new InvalidOperationException("The provided IVirtualScroll does not implement IVirtualScrollLayoutInfo interface.");

        return virtualScroll.ItemsLayout switch
        {
            LinearVirtualScrollLayout linearLayout => VirtualScrollPlatformLayoutFactory.CreateList(linearLayout, layoutInfo),
            _ => throw new NotSupportedException($"Layout type {virtualScroll.ItemsLayout.GetType().Name} is not supported.")
        };
    }

    /// <summary>
    /// Maps the item template property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapItemTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler.IsConnecting)
        {
            return;
        }
        
        var sectionCount = virtualScroll.Adapter?.GetSectionCount() ?? 0;
        if (sectionCount == 0)
        {
            return;
        }

        var collectionView = handler.CollectionView;
        collectionView.PerformBatchUpdates(collectionView.ReloadData, null!);
    }

    /// <summary>
    /// Maps the section header template property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapSectionHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll) => MapSectionTemplate(handler, virtualScroll);
    
    /// <summary>
    /// Maps the section footer template property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapSectionFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll) => MapSectionTemplate(handler, virtualScroll);

    private static void MapSectionTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler.IsConnecting)
        {
            return;
        }

        // TODO: Optimize instead of recreating layout
        MapLayout(handler, virtualScroll);
    }

    /// <summary>
    /// Maps the header property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapHeader(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler.IsConnecting)
        {
            return;
        }

        // TODO: Optimize instead of recreating layout
        MapLayout(handler, virtualScroll);

        // Optimization idea
        // var collectionView = handler.PlatformView;
        // if (collectionView.CollectionViewLayout is VirtualScrollCollectionViewLayout collectionViewLayout)
        // {
        //     var hasGlobalHeader = virtualScroll.Header is not null;
        //     if (collectionViewLayout.HasGlobalHeader != hasGlobalHeader)
        //     {
        //         MapLayout(handler, virtualScroll);
        //     }
        //     else
        //     {
        //         var indexPaths = collectionView.GetIndexPathsForVisibleSupplementaryElements(UICollectionElementKindSectionKey.Header);
        //         var invalidationContext = new UICollectionViewLayoutInvalidationContext();
        //         invalidationContext.InvalidateSupplementaryElements(UICollectionElementKindSectionKey.Header, indexPaths);
        //         collectionViewLayout.InvalidateLayout(invalidationContext);
        //     }
        // }
    }

    /// <summary>
    /// Maps the footer property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapFooter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler.IsConnecting)
        {
            return;
        }

        // TODO: Optimize instead of recreating layout
        MapLayout(handler, virtualScroll);
    }

    /// <summary>
    /// Maps the refresh accent color property from the virtual scroll to the platform refresh control.
    /// </summary>
    public static void MapRefreshAccentColor(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (virtualScroll.RefreshAccentColor is not null && handler._refreshControl is not null)
        {
            handler._refreshControl.TintColor = virtualScroll.RefreshAccentColor.ToPlatform();
        }
    }

    /// <summary>
    /// Maps the is refresh enabled property from the virtual scroll to the platform refresh control.
    /// </summary>
    public static void MapIsRefreshEnabled(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        var isRefreshEnabled = virtualScroll?.IsRefreshEnabled ?? false;
        var refreshControl = handler._refreshControl;
        if (refreshControl is not null)
        {
            refreshControl.Enabled = isRefreshEnabled;
            if (isRefreshEnabled && !ReferenceEquals(handler.CollectionView.RefreshControl, refreshControl))
            {
                handler.CollectionView.RefreshControl = refreshControl;
            }
            else if (!isRefreshEnabled && ReferenceEquals(handler.CollectionView.RefreshControl, refreshControl))
            {
                handler.CollectionView.RefreshControl = null;
            }
        }
    }

    /// <summary>
    /// Maps the is refreshing property from the virtual scroll to the platform refresh control.
    /// </summary>
    public static void MapIsRefreshing(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler._refreshControl is null || handler._isUpdatingIsRefreshingFromPlatform)
        {
            return;
        }

        var isRefreshing = virtualScroll.IsRefreshing;
        
        if (isRefreshing && !handler._refreshControl.Refreshing)
        {
            // Programmatically trigger refresh indicator
            handler._refreshControl.BeginRefreshing();
        }
        else if (!isRefreshing && handler._refreshControl.Refreshing)
        {
            // Stop refresh indicator
            handler._refreshControl.EndRefreshing();
        }
        
        // Sync platform state back to IsRefreshing (two-way binding)
        if (handler._refreshControl.Refreshing != isRefreshing)
        {
            handler._isUpdatingIsRefreshingFromPlatform = true;
            if (virtualScroll is VirtualScroll vs)
            {
                vs.IsRefreshing = handler._refreshControl.Refreshing;
            }
            handler._isUpdatingIsRefreshingFromPlatform = false;
        }
    }
}
