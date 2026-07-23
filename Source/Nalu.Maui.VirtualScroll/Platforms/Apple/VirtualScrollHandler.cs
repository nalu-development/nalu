using CoreFoundation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Controls.Handlers.Items;
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
    private nfloat? _refreshRevealRestoreOffsetY;
    private int _scrollToGeneration;
    private VirtualScrollDelegate? _delegate;
    private bool _isUpdatingIsRefreshingFromPlatform;
    private VirtualScrollCollectionView? _collectionView;
    private VirtualScrollContainerView? _containerView;
    private UILongPressGestureRecognizer? _dragGestureRecognizer;
    private CGSize _lastBounds;

    internal VirtualScrollPlatformDataSourceNotifier? Notifier => _notifier;

    /// <summary>
    /// Gets the <see cref="UICollectionView"/> platform view.
    /// </summary>
    /// <exception cref="InvalidOperationException">when the handler is not connected.</exception>
    public UICollectionView PlatformCollectionView => _collectionView ?? throw new InvalidOperationException("CollectionView has not been created.");

    /// <summary>
    /// Gets the range of currently visible items in the virtual scroll.
    /// </summary>
    /// <returns>A <see cref="VirtualScrollRange"/> containing the first and last visible item positions, or <c>null</c> if no items are visible.</returns>
    public VirtualScrollRange? GetVisibleItemsRange()
    {
        var collectionView = PlatformCollectionView;
        var layoutInfo = VirtualView as IVirtualScrollLayoutInfo;

        (int Section, int Item)? start = null;
        (int Section, int Item)? end = null;

        void UpdateRange(int section, int item)
        {
            if (!start.HasValue || (section, item).CompareTo(start.Value) < 0)
            {
                start = (section, item);
            }

            if (!end.HasValue || (section, item).CompareTo(end.Value) > 0)
            {
                end = (section, item);
            }
        }

        // Check global header
        if (layoutInfo?.HasGlobalHeader == true &&
            collectionView.GetIndexPathsForVisibleSupplementaryElements(VirtualScrollPlatformLayoutFactory.NSElementKindGlobalHeader).Length > 0)
        {
            UpdateRange(VirtualScrollRange.GlobalHeaderSectionIndex, 0);
        }

        // Check global footer
        if (layoutInfo?.HasGlobalFooter == true &&
            collectionView.GetIndexPathsForVisibleSupplementaryElements(VirtualScrollPlatformLayoutFactory.NSElementKindGlobalFooter).Length > 0)
        {
            UpdateRange(VirtualScrollRange.GlobalFooterSectionIndex, 0);
        }

        // Check section headers
        if (layoutInfo?.HasSectionHeader == true)
        {
            foreach (var indexPath in collectionView.GetIndexPathsForVisibleSupplementaryElements(VirtualScrollPlatformLayoutFactory.NSElementKindSectionHeader))
            {
                UpdateRange(indexPath.Section, VirtualScrollRange.SectionHeaderItemIndex);
            }
        }

        // Check section footers
        if (layoutInfo?.HasSectionFooter == true)
        {
            foreach (var indexPath in collectionView.GetIndexPathsForVisibleSupplementaryElements(VirtualScrollPlatformLayoutFactory.NSElementKindSectionFooter))
            {
                UpdateRange(indexPath.Section, VirtualScrollRange.SectionFooterItemIndex);
            }
        }

        // Check visible items
        foreach (var indexPath in collectionView.IndexPathsForVisibleItems)
        {
            UpdateRange(indexPath.Section, indexPath.Item.ToInt32());
        }

        if (!start.HasValue || !end.HasValue)
        {
            return null;
        }

        return new VirtualScrollRange(start.Value.Section, start.Value.Item, end.Value.Section, end.Value.Item);
    }

    /// <inheritdoc />
    protected override UIView CreatePlatformView()
    {
        var virtualScroll = VirtualView;
        var layoutSetup = CreatePlatformLayout(this, virtualScroll);
        var collectionView = new VirtualScrollCollectionView(CGRect.Empty, layoutSetup.Layout, (IVirtualScrollLayoutInfo)virtualScroll);
        var reuseIdManager = new VirtualScrollPlatformReuseIdManager(collectionView);

        layoutSetup.ConfigureCollectionView(collectionView);

        _reuseIdManager = reuseIdManager;
        _collectionView = collectionView;

        // Create refresh control
        _refreshControl = new UIRefreshControl();
        _refreshControl.Enabled = virtualScroll.IsRefreshEnabled;
        _refreshControl.AddTarget(RefreshControlEventHandler, UIControlEvent.ValueChanged);

        collectionView.AlwaysBounceVertical = true;
        collectionView.RefreshControl = _refreshControl;
        
        var scrollDirection = layoutSetup.Layout is VirtualScrollCollectionViewLayout virtualScrollLayout
            ? virtualScrollLayout.ScrollDirection
            : UICollectionViewScrollDirection.Vertical;
        
        _delegate = new VirtualScrollDelegate(virtualScroll, collectionView, scrollDirection, virtualScroll.FadingEdgeLength);
        collectionView.Delegate = _delegate;

        // Wrap collection view in a container view for fading edge support
        _containerView = new VirtualScrollContainerView();
        
        // Subscribe to events for fading edge updates
        _containerView.BoundsChanged += OnBoundsChanged;
        collectionView.ContentSizeChanged += OnContentSizeChanged;
        
        var containerView = _containerView;
        collectionView.TranslatesAutoresizingMaskIntoConstraints = false;
        containerView.AddSubview(collectionView);
        
        NSLayoutConstraint.ActivateConstraints([
            collectionView.TopAnchor.ConstraintEqualTo(containerView.TopAnchor),
            collectionView.LeadingAnchor.ConstraintEqualTo(containerView.LeadingAnchor),
            collectionView.TrailingAnchor.ConstraintEqualTo(containerView.TrailingAnchor),
            collectionView.BottomAnchor.ConstraintEqualTo(containerView.BottomAnchor)
        ]);

        return containerView;
    }

    private void RefreshControlEventHandler(object? sender, EventArgs e)
    {
        var virtualView = VirtualView;
        // User pulled to refresh - sync platform state to IsRefreshing first
        if (virtualView is VirtualScroll virtualScrollElement && _refreshControl is not null)
        {
            _isUpdatingIsRefreshingFromPlatform = true;
            virtualScrollElement.SetValueFromRenderer(VirtualScroll.IsRefreshingProperty, _refreshControl.Refreshing);
            _isUpdatingIsRefreshingFromPlatform = false;
        }
            
        // Then call Refresh() which will fire RefreshCommand/OnRefresh
        if (virtualView is IVirtualScrollController controller)
        {
            controller.Refresh(() => { /* Completion handled by IsRefreshing property */ });
        }
    }

    private static UILongPressGestureRecognizer CreateDragGestureRecognizer()
        => new(HandleLongPress);

    // ReSharper disable once MemberCanBeMadeStatic.Local
    private static void HandleLongPress(UILongPressGestureRecognizer gesture)
    {
        if (gesture.View is not VirtualScrollCollectionView cv)
        {
            return;
        }

        switch (gesture.State)
        {
            case UIGestureRecognizerState.Began:
            {
                var location = gesture.LocationInView(cv);
                var indexPath = cv.IndexPathForItemAtPoint(location);

                if (indexPath is not null && cv.CellForItem(indexPath) is { } cell)
                {
                    ((VirtualScrollDelegate) cv.Delegate).ItemDragInitiating(indexPath);
                    indexPath = cv.IndexPathForCell(cell);

                    if (indexPath is not null)
                    {
                        cv.BeginInteractiveMovementForItem(indexPath);
                    }
                }

                break;
            }
            case UIGestureRecognizerState.Changed:
            {
                var location = gesture.LocationInView(cv);
                cv.UpdateInteractiveMovement(location);

                break;
            }
            case UIGestureRecognizerState.Ended:
            {
                cv.EndInteractiveMovement();
                ((VirtualScrollDelegate) cv.Delegate).ItemDragEnded();

                break;
            }
            default:
            {
                cv.CancelInteractiveMovement();
                ((VirtualScrollDelegate) cv.Delegate).ItemDragEnded();

                break;
            }
        }
    }

    private void OnBoundsChanged(object? sender, EventArgs e)
    {
        if (_collectionView is not null)
        {
            _delegate?.UpdateFadingEdge(_collectionView);

            // Maintain carousel snap to the first page
            if (_collectionView.PagingEnabled && VirtualView?.ItemsLayout is { Orientation: var orientation })
            {
                var bounds = _collectionView.Bounds;
                var contentSize = _collectionView.ContentSize;
                var scrollX = _collectionView.ContentOffset.X;
                var scrollY = _collectionView.ContentOffset.Y;
                nfloat? newX = null, newY = null;

                if (orientation == ItemsLayoutOrientation.Horizontal)
                {
                    var pageWidth = bounds.Width;
                    if (pageWidth > 0 && _lastBounds.Width > 0 && contentSize.Width > 0)
                    {
                        var page = Math.Round(scrollX / _lastBounds.Width);
                        var snapX = (nfloat)(page * pageWidth);
                        var maxX = contentSize.Width - pageWidth;
                        newX = (nfloat)Math.Max(0, Math.Min(maxX, snapX));
                        newY = scrollY;
                    }
                }
                else
                {
                    var pageHeight = bounds.Height;
                    if (pageHeight > 0 && _lastBounds.Height > 0 && contentSize.Height > 0)
                    {
                        var page = Math.Round(scrollY / _lastBounds.Height);
                        var snapY = (nfloat)(page * pageHeight);
                        var maxY = contentSize.Height - pageHeight;
                        newX = scrollX;
                        newY = (nfloat)Math.Max(0, Math.Min(maxY, snapY));
                    }
                }

                if (newX is { } x && newY is { } y)
                {
                    _collectionView.SetContentOffset(new CGPoint(x, y), false);
                }
            }

            _lastBounds = _collectionView.Bounds.Size;
        }
    }
    private void OnContentSizeChanged(object? s, EventArgs e)
    {
        if (_collectionView is not null)
        {
            _delegate?.UpdateFadingEdge(_collectionView);
        }
    }

    /// <inheritdoc />
    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);
        
        // Initial fading edge update
        _delegate?.UpdateFadingEdge(_collectionView!);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(UIView platformView)
    {
        _notifier?.Dispose();
        _notifier = null;
        
        if (_refreshControl is not null)
        {
            _refreshControl.RemoveTarget(RefreshControlEventHandler, UIControlEvent.ValueChanged);
            _refreshControl.RemoveFromSuperview();
            _refreshControl.Dispose();
            _refreshControl = null;
        }
        
        // Unsubscribe from events
        if (_containerView is not null)
        {
            _containerView.BoundsChanged -= OnBoundsChanged;
        }
        
        if (_collectionView is not null)
        {
            _collectionView.ContentSizeChanged -= OnContentSizeChanged;
            var dataSource = _collectionView.DataSource;
            if (dataSource is not null)
            {
                dataSource.Dispose();
            }
            _collectionView.DataSource = null!;
            _collectionView.Delegate = null!;
            _collectionView.RefreshControl = null;
        }
        
        _delegate?.Dispose();
        _delegate = null;

        if (_dragGestureRecognizer is not null)
        {
            _collectionView?.RemoveGestureRecognizer(_dragGestureRecognizer);
            _dragGestureRecognizer.Dispose();
            _dragGestureRecognizer = null;
        }
        _collectionView?.Dispose();
        _collectionView = null;
        _containerView?.Dispose();
        _containerView = null;
        _lastBounds = CGSize.Empty;
        _reuseIdManager = null;

        base.DisconnectHandler(platformView);

        EnsureCreatedCellsCleanup();
    }

    /// <summary>
    /// Maps the drag handler property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapDragHandler(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        var collectionView = handler.PlatformCollectionView;
        var isDragEnabled = virtualScroll.DragHandler is not null;
        
        if (isDragEnabled)
        {
            handler._dragGestureRecognizer ??= CreateDragGestureRecognizer();
            collectionView.AddGestureRecognizer(handler._dragGestureRecognizer);
        }
        else if (handler._dragGestureRecognizer is not null)
        {
            collectionView.RemoveGestureRecognizer(handler._dragGestureRecognizer);
            handler._dragGestureRecognizer.Dispose();
            handler._dragGestureRecognizer = null;
        }
    }

    /// <summary>
    /// Maps the adapter property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapAdapter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Dispose existing notifier if any
        handler._notifier?.Dispose();
        handler._notifier = null;

        var collectionView = handler.PlatformCollectionView;
        var oldDataSource = collectionView.DataSource;

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

        if (oldDataSource is not null)
        {
            oldDataSource.Dispose();
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

        var layoutSetup = CreatePlatformLayout(handler, virtualScroll);
        var collectionView = handler.PlatformCollectionView;
        var animated = collectionView.Window is not null;
        collectionView.SetCollectionViewLayout(layoutSetup.Layout, animated);
        layoutSetup.ConfigureCollectionView(collectionView);
        
        // Update cached orientation and fading edge when layout changes (orientation might have changed)
        var scrollDirection = layoutSetup.Layout is VirtualScrollCollectionViewLayout virtualScrollLayout
            ? virtualScrollLayout.ScrollDirection
            : UICollectionViewScrollDirection.Vertical;
        
        handler._delegate?.UpdateOrientation(scrollDirection);
        if (handler._collectionView is not null)
        {
            handler._delegate?.UpdateFadingEdge(handler._collectionView);
        }
    }

    private struct VirtualScrollLayoutInfo : IVirtualScrollLayoutInfo
    {
        public VirtualScrollLayoutInfo(IVirtualScrollLayoutInfo virtualScroll)
        {
            HasGlobalFooter = virtualScroll.HasGlobalFooter;
            HasGlobalHeader = virtualScroll.HasGlobalHeader;
            HasSectionFooter = virtualScroll.HasSectionFooter;
            HasSectionHeader = virtualScroll.HasSectionHeader;
        }
        
        public bool Equals(IVirtualScrollLayoutInfo? other) =>
            other != null &&
            HasGlobalFooter == other.HasGlobalFooter &&
            HasGlobalHeader == other.HasGlobalHeader &&
            HasSectionFooter == other.HasSectionFooter &&
            HasSectionHeader == other.HasSectionHeader;
            

        public bool HasGlobalHeader { get; }
        public bool HasGlobalFooter { get; }
        public bool HasSectionHeader { get; }
        public bool HasSectionFooter { get; }
    }
    
    private static VirtualScrollCollectionViewLayoutSetup CreatePlatformLayout(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        var layoutInfo = new VirtualScrollLayoutInfo(
            virtualScroll as IVirtualScrollLayoutInfo ??
            throw new InvalidOperationException("The provided IVirtualScroll does not implement IVirtualScrollLayoutInfo interface.")
        );

        var layoutSetup = virtualScroll.ItemsLayout switch
        {
            LinearVirtualScrollLayout linearLayout => VirtualScrollPlatformLayoutFactory.CreateList(linearLayout, layoutInfo),
            CarouselVirtualScrollLayout carouselLayout => VirtualScrollPlatformLayoutFactory.CreateCarousel(carouselLayout, layoutInfo),
            _ => throw new NotSupportedException($"Layout type {virtualScroll.ItemsLayout.GetType().Name} is not supported.")
        };
        
        // Wire up layout update callback via FinalizeCollectionViewUpdates
        // Use WeakReference to avoid the delegate holding a strong reference to virtualScroll
        if (layoutSetup.Layout is VirtualScrollCollectionViewLayout vsLayout)
        {
            var weakVirtualScroll = new WeakReference<IVirtualScroll>(virtualScroll);
            vsLayout.OnLayoutUpdateCompleted = () =>
            {
                if (weakVirtualScroll.TryGetTarget(out var target) && target is IVirtualScrollController controller)
                {
                    controller.LayoutUpdateCompleted();
                }
            };
        }
        
        return layoutSetup;
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

        var collectionView = handler.PlatformCollectionView;
        collectionView.ReloadData();
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
    /// Maps the background property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapBackground(VirtualScrollHandler handler, IView view)
    {
        var collectionView = handler.PlatformCollectionView;
        if (view.Background.IsNullOrEmpty())
        {
            collectionView.RemoveBackgroundLayer();
            collectionView.BackgroundColor = null;
            return;
        }
        
        collectionView.UpdateBackground(view);
    }

    /// <summary>
    /// Maps the header template property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
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
        //     var hasGlobalHeader = virtualScroll.HeaderTemplate is not null;
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
    /// Maps the footer template property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
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
        var isRefreshEnabled = virtualScroll.IsRefreshEnabled;
        var refreshControl = handler._refreshControl;
        if (refreshControl is not null)
        {
            refreshControl.Enabled = isRefreshEnabled;
            if (isRefreshEnabled && !ReferenceEquals(handler.PlatformCollectionView.RefreshControl, refreshControl))
            {
                handler.PlatformCollectionView.RefreshControl = refreshControl;
            }
            else if (!isRefreshEnabled && ReferenceEquals(handler.PlatformCollectionView.RefreshControl, refreshControl))
            {
                handler.PlatformCollectionView.RefreshControl = null;
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
            // BeginRefreshing alone does NOT show the spinner: per Apple's recipe for
            // programmatic refresh, the scroll view must be scrolled to expose the control.
            // Capture the geometry BEFORE BeginRefreshing (which expands the top inset).
            var collectionView = handler.PlatformCollectionView;
            var topInset = collectionView.AdjustedContentInset.Top;
            var isAtTop = collectionView.ContentOffset.Y <= -topInset + 1;
            var controlHeight = handler._refreshControl.Frame.Height > 0 ? handler._refreshControl.Frame.Height : 60;

            handler._refreshControl.BeginRefreshing();

            // Only auto-reveal when the list rests at the top: revealing from a scrolled
            // position would yank the content away from what the user is reading.
            if (isAtTop)
            {
                handler._refreshRevealRestoreOffsetY = collectionView.ContentOffset.Y;
                collectionView.SetContentOffset(new CGPoint(collectionView.ContentOffset.X, -(controlHeight + topInset)), true);
            }
        }
        else if (!isRefreshing && handler._refreshControl.Refreshing)
        {
            // Stop refresh indicator
            handler._refreshControl.EndRefreshing();

            // Restore the exact pre-reveal offset once EndRefreshing's own asynchronous
            // adjustments (inset removal + offset compensation) have settled — restoring
            // earlier races them and lands offset by the control height.
            if (handler._refreshRevealRestoreOffsetY is { } restoreOffsetY)
            {
                handler._refreshRevealRestoreOffsetY = null;
                handler.RestoreOffsetWhenSettled(restoreOffsetY, handler._collectionView?.ContentOffset ?? CGPoint.Empty, checksLeft: 25);
            }
        }
        
        // Sync platform state back to IsRefreshing (two-way binding)
        if (handler._refreshControl.Refreshing != isRefreshing)
        {
            handler._isUpdatingIsRefreshingFromPlatform = true;
            if (virtualScroll is VirtualScroll vs)
            {
                vs.SetValueFromRenderer(VirtualScroll.IsRefreshingProperty, handler._refreshControl.Refreshing);
            }
            handler._isUpdatingIsRefreshingFromPlatform = false;
        }
    }

    /// <summary>
    /// Maps the ScrollTo command from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapScrollTo(VirtualScrollHandler handler, IVirtualScroll virtualScroll, object? args)
    {
        if (args is not VirtualScrollCommandScrollToArgs scrollToArgs)
        {
            return;
        }

        var sectionIndex = scrollToArgs.SectionIndex;
        var itemIndex = scrollToArgs.ItemIndex;
        var position = scrollToArgs.Position;
        var animated = scrollToArgs.Animated;

        // Global header/footer targets are addressed with the VirtualScrollRange sentinel
        // section indices (GlobalHeaderSectionIndex / GlobalFooterSectionIndex).
        if (sectionIndex is VirtualScrollRange.GlobalHeaderSectionIndex or VirtualScrollRange.GlobalFooterSectionIndex)
        {
            var globalLayoutInfo = virtualScroll as IVirtualScrollLayoutInfo;
            var isGlobalHeader = sectionIndex == VirtualScrollRange.GlobalHeaderSectionIndex;

            if (isGlobalHeader ? globalLayoutInfo?.HasGlobalHeader == true : globalLayoutInfo?.HasGlobalFooter == true)
            {
                handler.ScrollToSupplementary(
                    isGlobalHeader ? SupplementaryScrollTarget.GlobalHeader : SupplementaryScrollTarget.GlobalFooter,
                    0,
                    0,
                    position,
                    animated
                );
            }

            return;
        }

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

        var collectionView = handler.PlatformCollectionView;
        NSIndexPath indexPath;

        // If itemIndex is -1, scroll to section header (or first item if no header)
        if (itemIndex == -1)
        {
            var layoutInfo = virtualScroll as IVirtualScrollLayoutInfo;

            // Check if section headers are enabled
            if (layoutInfo?.HasSectionHeader == true)
            {
                handler.ScrollToSectionHeader(sectionIndex, position, animated);
                return;
            }

            // Fallback: scroll to first item in section (no header configured)
            var itemCount = virtualScroll.Adapter.GetItemCount(sectionIndex);
            if (itemCount == 0)
            {
                return;
            }

            indexPath = NSIndexPath.FromItemSection(0, sectionIndex);
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

            indexPath = NSIndexPath.FromItemSection(itemIndex, sectionIndex);
        }
        
        var targetSection = (int) indexPath.Section;
        var targetItem = (int) indexPath.Item;

        if (position == ScrollToPosition.MakeVisible)
        {
            // MakeVisible is path-dependent (minimal scroll): UICollectionView.ScrollToItem
            // over/undershoots with estimated far-away sizes and cannot be corrected afterwards
            // without violating the "don't move when already visible" contract. Compute the
            // offset directly instead, refining as the region gets measured.
            handler.ScrollToSupplementary(SupplementaryScrollTarget.Item, targetSection, targetItem, position, animated);

            return;
        }

        // Convert ScrollToPosition to UICollectionViewScrollPosition
        var scrollPosition = position.ToCollectionViewScrollPosition(GetScrollDirection(collectionView), false);
        var originalOffset = collectionView.ContentOffset;
        var generation = ++handler._scrollToGeneration;

        collectionView.ScrollToItem(indexPath, scrollPosition, animated);

        // Estimated far-away sizes make ScrollToItem land imprecisely: refine once settled.
        handler.AdjustOffsetToSupplementaryWhenSettled(SupplementaryScrollTarget.Item, targetSection, targetItem, position, originalOffset, collectionView.ContentOffset, checksLeft: 25, generation);
    }

    private void RestoreOffsetWhenSettled(nfloat restoreOffsetY, CGPoint lastOffset, int checksLeft)
        => DispatchQueue.MainQueue.DispatchAfter(
            new DispatchTime(DispatchTime.Now, 80_000_000L /* 80ms in ns */),
            () =>
            {
                if (_collectionView is not { } collectionView)
                {
                    return;
                }

                var offset = collectionView.ContentOffset;

                if (Math.Abs(offset.Y - lastOffset.Y) >= 0.5 && checksLeft > 0)
                {
                    RestoreOffsetWhenSettled(restoreOffsetY, offset, checksLeft - 1);

                    return;
                }

                // Skip when the user (or the platform) already moved past the target.
                if (offset.Y < restoreOffsetY - 0.5)
                {
                    collectionView.SetContentOffset(new CGPoint(offset.X, restoreOffsetY), true);
                }
            }
        );

    private enum SupplementaryScrollTarget
    {
        SectionHeader,
        GlobalHeader,
        GlobalFooter,
        Item
    }

    private static NSString GetSupplementaryElementKind(SupplementaryScrollTarget target) => target switch
    {
        SupplementaryScrollTarget.GlobalHeader => VirtualScrollPlatformLayoutFactory.NSElementKindGlobalHeader,
        SupplementaryScrollTarget.GlobalFooter => VirtualScrollPlatformLayoutFactory.NSElementKindGlobalFooter,
        _ => VirtualScrollPlatformLayoutFactory.NSElementKindSectionHeader
    };

    /// <summary>
    /// Scrolls so that the header of the given section satisfies <paramref name="position"/>.
    /// </summary>
    /// <remarks>
    /// With self-sizing cells the layout attributes of far-away supplementary elements are based
    /// on estimated sizes (and the compositional layout re-anchors the content offset while cells
    /// get measured), so a single SetContentOffset to the header frame lands short.
    /// <see cref="UICollectionView.ScrollToItem"/> does compensate for estimation, so we first
    /// scroll to the section's first item, wait for the offset to settle, then fix up the offset
    /// using the now-measured header frame (iterating, as each jump can re-measure cells).
    /// </remarks>
    private void ScrollToSectionHeader(int sectionIndex, ScrollToPosition position, bool animated)
    {
        var collectionView = PlatformCollectionView;
        var itemCount = VirtualView?.Adapter?.GetItemCount(sectionIndex) ?? 0;

        // MakeVisible is path-dependent: anchoring the section's first item first would
        // over-scroll and could not be undone. Use the direct-offset route instead.
        if (itemCount == 0 || position == ScrollToPosition.MakeVisible)
        {
            ScrollToSupplementary(SupplementaryScrollTarget.SectionHeader, sectionIndex, 0, position, animated);

            return;
        }

        var scrollDirection = GetScrollDirection(collectionView);
        var scrollPosition = position.ToCollectionViewScrollPosition(scrollDirection, false);
        var originalOffset = collectionView.ContentOffset;
        var generation = ++_scrollToGeneration;
        collectionView.ScrollToItem(NSIndexPath.FromItemSection(0, sectionIndex), scrollPosition, animated);

        // 25 x 80ms ≈ 2s upper bound for the settle wait (animated scrolls take ~300-500ms).
        AdjustOffsetToSupplementaryWhenSettled(SupplementaryScrollTarget.SectionHeader, sectionIndex, 0, position, originalOffset, collectionView.ContentOffset, checksLeft: 25, generation);
    }

    /// <summary>
    /// Scrolls so that the given element satisfies <paramref name="position"/> by computing the
    /// content offset directly from its layout attributes.
    /// </summary>
    /// <remarks>
    /// The first hop may rely on estimate-based attributes; the settle + adjust passes then
    /// converge on the exact offset as the region gets measured.
    /// </remarks>
    private void ScrollToSupplementary(SupplementaryScrollTarget target, int sectionIndex, int itemIndex, ScrollToPosition position, bool animated)
    {
        var collectionView = PlatformCollectionView;

        // MakeVisible semantics must be evaluated against the offset the gesture STARTED from:
        // the estimate-based first hop may move the target into view, and deciding against the
        // moved offset would freeze there instead of converging on the minimal-scroll edge.
        var originalOffset = collectionView.ContentOffset;
        var generation = ++_scrollToGeneration;
        AdjustOffsetToSupplementary(target, sectionIndex, itemIndex, position, originalOffset, animated, attemptsLeft: 1, generation);
        AdjustOffsetToSupplementaryWhenSettled(target, sectionIndex, itemIndex, position, originalOffset, collectionView.ContentOffset, checksLeft: 25, generation);
    }

    private void AdjustOffsetToSupplementaryWhenSettled(SupplementaryScrollTarget target, int sectionIndex, int itemIndex, ScrollToPosition position, CGPoint originalOffset, CGPoint lastOffset, int checksLeft, int generation)
        => DispatchQueue.MainQueue.DispatchAfter(
            new DispatchTime(DispatchTime.Now, 80_000_000L /* 80ms in ns */),
            () =>
            {
                // A newer ScrollTo command supersedes this chain.
                if (generation != _scrollToGeneration || _collectionView is not { } collectionView)
                {
                    return;
                }

                var offset = collectionView.ContentOffset;
                var settled = Math.Abs(offset.X - lastOffset.X) < 0.5 && Math.Abs(offset.Y - lastOffset.Y) < 0.5;

                if (settled || checksLeft <= 0)
                {
                    AdjustOffsetToSupplementary(target, sectionIndex, itemIndex, position, originalOffset, animated: false, attemptsLeft: 3, generation);
                }
                else
                {
                    AdjustOffsetToSupplementaryWhenSettled(target, sectionIndex, itemIndex, position, originalOffset, offset, checksLeft - 1, generation);
                }
            }
        );

    private void AdjustOffsetToSupplementary(SupplementaryScrollTarget target, int sectionIndex, int itemIndex, ScrollToPosition position, CGPoint originalOffset, bool animated, int attemptsLeft, int generation)
    {
        // A newer ScrollTo command supersedes this chain.
        if (generation != _scrollToGeneration
            || _collectionView is not { } collectionView
            || VirtualView?.Adapter is not { } adapter)
        {
            return;
        }

        collectionView.LayoutIfNeeded();

        UICollectionViewLayoutAttributes? attributes;

        if (target == SupplementaryScrollTarget.Item)
        {
            if (sectionIndex >= adapter.GetSectionCount() || itemIndex >= adapter.GetItemCount(sectionIndex))
            {
                return;
            }

            attributes = collectionView.GetLayoutAttributesForItem(NSIndexPath.FromItemSection(itemIndex, sectionIndex));

            if (attributes is null)
            {
                return;
            }
        }
        else if (target == SupplementaryScrollTarget.SectionHeader && sectionIndex >= adapter.GetSectionCount())
        {
            return;
        }
        else
        {
            // Global boundary items are addressed with a single-index path, section items with (item, section).
            var indexPath = target == SupplementaryScrollTarget.SectionHeader
                ? NSIndexPath.FromItemSection(0, sectionIndex)
                : NSIndexPath.FromIndex(0);

            attributes = collectionView.GetLayoutAttributesForSupplementaryElement(
                GetSupplementaryElementKind(target),
                indexPath);
        }

        if (attributes is null)
        {
            // The layout may not provide attributes for far-away boundary elements yet:
            // jump towards the content extreme where the element lives; the following
            // settle/adjust passes refine the offset once the region is measured.
            if (target != SupplementaryScrollTarget.SectionHeader)
            {
                var extremeOffset = collectionView.ContentOffset;
                var extremeInset = collectionView.AdjustedContentInset;

                if (GetScrollDirection(collectionView) == UICollectionViewScrollDirection.Vertical)
                {
                    extremeOffset.Y = target == SupplementaryScrollTarget.GlobalHeader
                        ? (nfloat) (-extremeInset.Top)
                        : (nfloat) Math.Max(collectionView.ContentSize.Height + extremeInset.Bottom - collectionView.Bounds.Height, -extremeInset.Top);
                }
                else
                {
                    extremeOffset.X = target == SupplementaryScrollTarget.GlobalHeader
                        ? (nfloat) (-extremeInset.Left)
                        : (nfloat) Math.Max(collectionView.ContentSize.Width + extremeInset.Right - collectionView.Bounds.Width, -extremeInset.Left);
                }

                collectionView.SetContentOffset(extremeOffset, animated);
            }

            return;
        }

        var contentOffset = collectionView.ContentOffset;
        var inset = collectionView.AdjustedContentInset;
        double targetOffset;
        double current;

        if (GetScrollDirection(collectionView) == UICollectionViewScrollDirection.Vertical)
        {
            current = contentOffset.Y;
            targetOffset = ComputeSupplementaryTargetOffset(
                position, attributes.Frame.Y, attributes.Frame.Height, originalOffset.Y,
                collectionView.Bounds.Height, inset.Top, inset.Bottom, collectionView.ContentSize.Height);
        }
        else
        {
            current = contentOffset.X;
            targetOffset = ComputeSupplementaryTargetOffset(
                position, attributes.Frame.X, attributes.Frame.Width, originalOffset.X,
                collectionView.Bounds.Width, inset.Left, inset.Right, collectionView.ContentSize.Width);
        }

        if (Math.Abs(targetOffset - current) < 0.5)
        {
            return;
        }

        if (GetScrollDirection(collectionView) == UICollectionViewScrollDirection.Vertical)
        {
            contentOffset.Y = (nfloat) targetOffset;
        }
        else
        {
            contentOffset.X = (nfloat) targetOffset;
        }

        collectionView.SetContentOffset(contentOffset, animated);

        if (attemptsLeft > 1)
        {
            // The jump may materialize cells whose measured sizes shift the layout again.
            DispatchQueue.MainQueue.DispatchAsync(() => AdjustOffsetToSupplementary(target, sectionIndex, itemIndex, position, originalOffset, animated: false, attemptsLeft - 1, generation));
        }
    }

    /// <remarks>
    /// <paramref name="referenceOffset"/> is the offset the ScrollTo gesture STARTED from —
    /// MakeVisible ("minimal scroll") is defined relative to it, so that the iterative
    /// estimate-correcting hops neither freeze mid-way nor move when the element was
    /// already visible at the start.
    /// </remarks>
    private static double ComputeSupplementaryTargetOffset(
        ScrollToPosition position,
        double elementStart,
        double elementSize,
        double referenceOffset,
        double viewportSize,
        double leadingInset,
        double trailingInset,
        double contentSize)
    {
        var minOffset = -leadingInset;
        var maxOffset = Math.Max(contentSize + trailingInset - viewportSize, minOffset);
        var visibleSize = viewportSize - leadingInset - trailingInset;

        var target = position switch
        {
            ScrollToPosition.Center => elementStart + (elementSize / 2) - leadingInset - (visibleSize / 2),
            ScrollToPosition.End => elementStart + elementSize - viewportSize + trailingInset,
            ScrollToPosition.MakeVisible when elementStart >= referenceOffset + leadingInset
                                              && elementStart + elementSize <= referenceOffset + viewportSize - trailingInset
                => referenceOffset,
            ScrollToPosition.MakeVisible when elementStart + elementSize > referenceOffset + viewportSize - trailingInset
                                              && elementStart >= referenceOffset + leadingInset
                => elementStart + elementSize - viewportSize + trailingInset,
            _ => elementStart - leadingInset
        };

        return Math.Clamp(target, minOffset, maxOffset);
    }

    private static UICollectionViewScrollDirection GetScrollDirection(UICollectionView collectionView)
        => collectionView.CollectionViewLayout is VirtualScrollCollectionViewLayout layout
            ? layout.ScrollDirection
            : UICollectionViewScrollDirection.Vertical;

    /// <summary>
    /// Maps the scroll event enabled state from the virtual scroll to the platform collection view delegate.
    /// </summary>
    public static void MapSetScrollEventEnabled(VirtualScrollHandler handler, IVirtualScroll virtualScroll, object? args)
    {
        if (args is not bool enabled)
        {
            return;
        }

        // Delegate is always attached, just enable/disable scroll event notifications
        handler._delegate?.SetScrollEventsEnabled(enabled);
    }

    /// <summary>
    /// Maps the fading edge length property from the virtual scroll to the platform collection view.
    /// </summary>
    public static void MapFadingEdgeLength(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (virtualScroll is VirtualScroll vs && handler._collectionView is not null)
        {
            handler._delegate?.UpdateFadingEdgeLength(handler._collectionView, vs.FadingEdgeLength);
        }
    }
}
