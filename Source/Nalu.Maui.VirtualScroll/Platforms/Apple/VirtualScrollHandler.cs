using CoreGraphics;
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

        return collectionView;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(UIView platformView)
    {
        _notifier?.Dispose();
        _notifier = null;
        
        base.DisconnectHandler(platformView);
        var collectionView = (VirtualScrollCollectionView)platformView;
        collectionView.DataSource = null!;
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
}
