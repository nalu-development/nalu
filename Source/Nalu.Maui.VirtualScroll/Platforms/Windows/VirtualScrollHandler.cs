using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Layout = Microsoft.UI.Xaml.Controls.Layout;
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
using WGrid = Microsoft.UI.Xaml.Controls.Grid;
using WStackLayout = Microsoft.UI.Xaml.Controls.StackLayout;
using WRect = Windows.Foundation.Rect;

namespace Nalu;

#pragma warning disable IDE0060

// ReSharper disable UnusedParameter.Local
/// <summary>
/// Handler for the <see cref="VirtualScroll" /> view on Windows.
/// </summary>
public partial class VirtualScrollHandler
{
    private ItemsRepeaterScrollHost? _itemsRepeaterScrollHost;
    private ScrollViewer? _scrollViewer;
    private ItemsRepeater? _itemsRepeater;
    private VirtualScrollElementFactory? _elementFactory;
    private VirtualScrollItemsSource? _itemsSource;
    private VirtualScrollPlatformFlattenedAdapterNotifier? _notifier;
    private VirtualScrollPlatformReuseIdManager? _reuseIdManager;
    private Layout? _layout;
    private WGrid? _rootLayout;
    private IVirtualScrollFlattenedAdapter? _flattenedAdapter;
    private bool _isUpdatingIsRefreshingFromPlatform = false;
    private VirtualScrollDragDropHelper? _dragDropHelper;
    private bool _isSnapping = false;
    private bool _wasScrolling = false;

    /// <inheritdoc />
    protected override PlatformView CreatePlatformView()
    {
        _rootLayout = new VirtualScrollPlatformView();

        _itemsRepeaterScrollHost = new ItemsRepeaterScrollHost();
        _scrollViewer = new ScrollViewer();
        _itemsRepeater = new ItemsRepeater();

        // Use StackLayout for linear layouts, will be replaced in MapLayout if carousel
        _layout = new WStackLayout { Orientation = Orientation.Vertical };
        _itemsRepeater.Layout = _layout;

        _scrollViewer.Content = _itemsRepeater;
        _itemsRepeaterScrollHost.ScrollViewer = _scrollViewer;

        _itemsRepeaterScrollHost.Loaded += ItemsRepeaterScrollHost_Loaded;

        _rootLayout.Children.Add(_itemsRepeaterScrollHost);

        return _rootLayout;
    }

    private void ItemsRepeaterScrollHost_Loaded(object sender, RoutedEventArgs e)
    {
        _itemsRepeaterScrollHost!.Loaded -= ItemsRepeaterScrollHost_Loaded;

        // Register scroll event handlers
        _scrollViewer?.RegisterPropertyChangedCallback(ScrollViewer.VerticalOffsetProperty, OnScrollChanged);
        _scrollViewer?.RegisterPropertyChangedCallback(ScrollViewer.HorizontalOffsetProperty, OnScrollChanged);
        
        // Subscribe to ViewChanged for carousel snapping
        if (_scrollViewer is not null)
        {
            _scrollViewer.ViewChanged += OnScrollViewerViewChanged;
        }
    }
    
    private void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_scrollViewer is null || VirtualView is not IVirtualScrollController controller)
        {
            return;
        }

        var scrollX = _scrollViewer.HorizontalOffset;
        var scrollY = _scrollViewer.VerticalOffset;
        var totalWidth = _scrollViewer.ScrollableWidth;
        var totalHeight = _scrollViewer.ScrollableHeight;

        // When scrolling ends (IsIntermediate becomes false), fire ScrollEnded
        if (!e.IsIntermediate && _wasScrolling)
        {
            _wasScrolling = false;
            controller.ScrollEnded(scrollX, scrollY, totalWidth, totalHeight);
        }

        // Handle carousel snapping when scrolling has completed
        if (!e.IsIntermediate && !_isSnapping && VirtualView is IVirtualScroll virtualScroll)
        {
            // Only apply snapping for carousel layouts
            if (virtualScroll.ItemsLayout is CarouselVirtualScrollLayout carouselLayout)
            {
                // Calculate the nearest item position and snap to it
                SnapToNearestItem(carouselLayout.Orientation);
            }
        }
    }
    
    private void SnapToNearestItem(ItemsLayoutOrientation orientation)
    {
        if (_scrollViewer is null || _itemsRepeater is null || _elementFactory is null)
        {
            return;
        }

        _isSnapping = true;

        try
        {
            var realizedElements = _elementFactory.RealizedElements;
            if (realizedElements.Count == 0)
            {
                return;
            }

            double currentOffset;
            
            if (orientation == ItemsLayoutOrientation.Vertical)
            {
                currentOffset = _scrollViewer.VerticalOffset;
            }
            else
            {
                currentOffset = _scrollViewer.HorizontalOffset;
            }

            // Find the nearest realized element based on its actual position
            VirtualScrollElementContainer? nearestElement = null;
            var minDistance = double.MaxValue;

            foreach (var element in realizedElements)
            {
                if (element.FlattenedIndex < 0)
                {
                    continue;
                }

                // Get the element's position relative to the ItemsRepeater
                var transform = element.TransformToVisual(_itemsRepeater);
                var bounds = transform.TransformBounds(new WRect(0, 0, element.ActualWidth, element.ActualHeight));

                double elementPosition;
                if (orientation == ItemsLayoutOrientation.Vertical)
                {
                    elementPosition = bounds.Y;
                }
                else
                {
                    elementPosition = bounds.X;
                }

                // Calculate distance from current scroll offset
                var distance = Math.Abs(elementPosition - currentOffset);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestElement = element;
                }
            }

            if (nearestElement is null)
            {
                return;
            }

            // Get the target position for the nearest element
            var targetTransform = nearestElement.TransformToVisual(_itemsRepeater);
            var targetBounds = targetTransform.TransformBounds(new WRect(0, 0, nearestElement.ActualWidth, nearestElement.ActualHeight));

            double targetOffset;
            if (orientation == ItemsLayoutOrientation.Vertical)
            {
                targetOffset = targetBounds.Y;
            }
            else
            {
                targetOffset = targetBounds.X;
            }

            // Only snap if we're not already at the target (to avoid infinite loops)
            if (Math.Abs(currentOffset - targetOffset) > 1.0)
            {
                if (orientation == ItemsLayoutOrientation.Vertical)
                {
                    _scrollViewer.ChangeView(null, targetOffset, null, disableAnimation: false);
                }
                else
                {
                    _scrollViewer.ChangeView(targetOffset, null, null, disableAnimation: false);
                }
            }
        }
        finally
        {
            _isSnapping = false;
        }
    }

    private void OnScrollChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (_scrollViewer is null || VirtualView is not IVirtualScrollController controller)
        {
            return;
        }

        var scrollX = _scrollViewer.HorizontalOffset;
        var scrollY = _scrollViewer.VerticalOffset;
        var totalWidth = _scrollViewer.ScrollableWidth;
        var totalHeight = _scrollViewer.ScrollableHeight;

        // Check if scrolling has started
        if (!_wasScrolling)
        {
            _wasScrolling = true;
            controller.ScrollStarted(scrollX, scrollY, totalWidth, totalHeight);
        }

        controller.Scrolled(scrollX, scrollY, totalWidth, totalHeight);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        _notifier?.Dispose();
        _notifier = null;

        _elementFactory?.Dispose();
        _elementFactory = null;

        _itemsRepeater?.ItemsSource = null;
        
        if (_itemsSource is not null)
        {
            _itemsSource.CollectionChanged -= ItemsSourceOnCollectionChanged;
        }
        _itemsSource = null;
        _flattenedAdapter = null;
        _reuseIdManager = null;
        _dragDropHelper?.Dispose();
        _dragDropHelper = null;

        if (_itemsRepeaterScrollHost is not null)
        {
            _itemsRepeaterScrollHost.Loaded -= ItemsRepeaterScrollHost_Loaded;
        }

        if (_scrollViewer is not null)
        {
            _scrollViewer.ViewChanged -= OnScrollViewerViewChanged;
        }

        _itemsRepeater = null;
        _scrollViewer = null;
        _itemsRepeaterScrollHost = null;
        _layout = null;
        _rootLayout = null;

        base.DisconnectHandler(platformView);

        EnsureCreatedCellsCleanup();
    }

    /// <summary>
    /// Gets the underlying ItemsRepeater instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">when the handler is not connected.</exception>
    public ItemsRepeater PlatformItemsRepeater => _itemsRepeater ?? throw new InvalidOperationException("ItemsRepeater has not been created.");

    /// <summary>
    /// Gets the range of currently visible items in the virtual scroll.
    /// </summary>
    /// <returns>A <see cref="VirtualScrollRange"/> containing the first and last visible item positions, or <c>null</c> if no items are visible.</returns>
    public VirtualScrollRange? GetVisibleItemsRange()
    {
        if (_itemsRepeater is null || _flattenedAdapter is null || _elementFactory is null || _scrollViewer is null)
        {
            return null;
        }

        var realizedElements = _elementFactory.RealizedElements;
        if (!realizedElements.Any())
        {
            return null;
        }

        (int Section, int Item)? start = null;
        (int Section, int Item)? end = null;

        // Filter realized elements by viewport visibility
        foreach (var container in realizedElements)
        {
            if (container.FlattenedIndex < 0)
            {
                continue;
            }

            // Check if element is visible in the viewport
            if (!IsElementVisibleInViewport(container))
            {
                continue;
            }

            var position = GetPositionFromFlattenedIndex(container.FlattenedIndex);
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

    private bool IsElementVisibleInViewport(VirtualScrollElementContainer element)
    {
        if (_scrollViewer is null || _itemsRepeater is null)
        {
            return false;
        }

        try
        {
            // Get element bounds relative to ItemsRepeater
            var transform = element.TransformToVisual(_itemsRepeater);
            var elementBounds = transform.TransformBounds(new WRect(0.0, 0.0, element.ActualWidth, element.ActualHeight));

            // Get ScrollViewer viewport bounds relative to ItemsRepeater
            var scrollViewerTransform = _scrollViewer.TransformToVisual(_itemsRepeater);
            var viewportBounds = scrollViewerTransform.TransformBounds(new WRect(0.0, 0.0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight));

            // Check if element intersects with viewport
            return elementBounds.Left < viewportBounds.Right
                && elementBounds.Right > viewportBounds.Left
                && elementBounds.Top < viewportBounds.Bottom
                && elementBounds.Bottom > viewportBounds.Top;
        }
        catch
        {
            return false;
        }
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
    /// Maps the adapter property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapAdapter(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Dispose existing notifier if any
        handler._notifier?.Dispose();
        handler._notifier = null;

        var itemsRepeater = handler.PlatformItemsRepeater;

        if (virtualScroll.Adapter is { } adapter)
        {
            var mauiContext = handler.MauiContext ?? throw new InvalidOperationException("MauiContext cannot be null when mapping the Adapter.");
            var layoutInfo = virtualScroll as IVirtualScrollLayoutInfo ?? throw new InvalidOperationException("VirtualScroll must implement IVirtualScrollLayoutInfo.");
            var flattenedAdapter = new VirtualScrollFlattenedAdapter(adapter, layoutInfo);
            handler._flattenedAdapter = flattenedAdapter;

            handler._reuseIdManager = new VirtualScrollPlatformReuseIdManager();
            handler._elementFactory = new VirtualScrollElementFactory(mauiContext, virtualScroll, handler._reuseIdManager);
            itemsRepeater.ItemTemplate = handler._elementFactory;

            // Unsubscribe from previous itemsSource if any
            if (handler._itemsSource is not null)
            {
                handler._itemsSource.CollectionChanged -= handler.ItemsSourceOnCollectionChanged;
            }

            var itemsSource = new VirtualScrollItemsSource(flattenedAdapter);
            handler._itemsSource = itemsSource;
            itemsSource.CollectionChanged += handler.ItemsSourceOnCollectionChanged;
            itemsRepeater.ItemsSource = handler._itemsSource;

            // Create a new notifier instance every time the adapter changes to ensure a fresh subscription
            handler._notifier = new VirtualScrollPlatformFlattenedAdapterNotifier(handler._itemsSource, flattenedAdapter);
        }
        else
        {
            itemsRepeater.ItemTemplate = null;
            itemsRepeater.ItemsSource = null;
            handler._elementFactory?.Dispose();
            handler._elementFactory = null;
            
            if (handler._itemsSource is not null)
            {
                handler._itemsSource.CollectionChanged -= handler.ItemsSourceOnCollectionChanged;
            }
            handler._itemsSource = null;
            handler._flattenedAdapter = null;
            handler._reuseIdManager = null;
        }
    }

    private void ItemsSourceOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_elementFactory is null)
        {
            return;
        }

        // adjust items indexes accordingly
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var element in _elementFactory.RealizedElements)
                {
                    if (element.FlattenedIndex >= e.NewStartingIndex)
                    {
                        element.FlattenedIndex += e.NewItems?.Count ?? 1;
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                var removeCount = e.OldItems?.Count ?? 1;
                var removeStartIndex = e.OldStartingIndex;
                var removeEndIndex = removeStartIndex + removeCount - 1;
                
                foreach (var element in _elementFactory.RealizedElements)
                {
                    var index = element.FlattenedIndex;
                    
                    // If element is in the removed range, invalidate it
                    if (index >= removeStartIndex && index <= removeEndIndex)
                    {
                        element.FlattenedIndex = -1;
                    }
                    // If element is after the removed range, shift it back
                    else if (index > removeEndIndex)
                    {
                        element.FlattenedIndex -= removeCount;
                    }
                }
                break;
            case NotifyCollectionChangedAction.Replace:
                // No index changes needed for replace - items are swapped in place
                break;
            case NotifyCollectionChangedAction.Move:
                var moveCount = e.OldItems?.Count ?? 1;
                var oldStartIndex = e.OldStartingIndex;
                var oldEndIndex = oldStartIndex + moveCount - 1;
                var newStartIndex = e.NewStartingIndex;
                var newEndIndex = newStartIndex + moveCount - 1;
                
                // Use ToList to avoid modifying collection while iterating
                var elements = _elementFactory.RealizedElements.ToList();
                
                foreach (var element in elements)
                {
                    var index = element.FlattenedIndex;
                    
                    // Elements that were in the moved range get updated to new position
                    // But skip if already in destination range (to avoid double-processing)
                    if (index >= oldStartIndex && index <= oldEndIndex)
                    {
                        // Check if this element is already at the destination (shouldn't happen, but safety check)
                        if (index >= newStartIndex && index <= newEndIndex)
                        {
                            continue;
                        }
                        
                        var offset = index - oldStartIndex;
                        element.FlattenedIndex = newStartIndex + offset;
                    }
                    // Elements between old and new positions need to shift
                    else if (oldStartIndex < newStartIndex)
                    {
                        // Moving forward: elements between old and new positions shift backward
                        if (index > oldEndIndex && index <= newEndIndex)
                        {
                            element.FlattenedIndex -= moveCount;
                        }
                    }
                    else // oldStartIndex > newStartIndex
                    {
                        // Moving backward: elements at and between new and old positions shift forward
                        // Elements from newStartIndex (inclusive) to oldStartIndex (exclusive) shift forward
                        if (index >= newStartIndex && index < oldStartIndex)
                        {
                            element.FlattenedIndex += moveCount;
                        }
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                // On reset, invalidate all realized elements' indexes
                // The ItemsRepeater will handle rebuilding the view
                foreach (var element in _elementFactory.RealizedElements)
                {
                    element.FlattenedIndex = -1;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Maps the drag handler property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapDragHandler(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        var itemsRepeater = handler.PlatformItemsRepeater;
        if (virtualScroll.DragHandler is not null)
        {
            handler._dragDropHelper?.Dispose();
            handler._dragDropHelper = new VirtualScrollDragDropHelper(itemsRepeater, virtualScroll, handler._flattenedAdapter);
        }
        else
        {
            handler._dragDropHelper?.Dispose();
            handler._dragDropHelper = null;
        }
    }

    /// <summary>
    /// Maps the layout property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapLayout(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler._itemsRepeater is null || handler._scrollViewer is null)
        {
            return;
        }

        switch (virtualScroll.ItemsLayout)
        {
            case LinearVirtualScrollLayout linearLayout:
                // For linear layouts, use StackLayout
                if (handler._layout is not WStackLayout stackLayout)
                {
                    stackLayout = new WStackLayout();
                    handler._layout = stackLayout;
                    handler._itemsRepeater.Layout = stackLayout;
                }
                
                stackLayout.Orientation = linearLayout.Orientation == ItemsLayoutOrientation.Vertical
                    ? Orientation.Vertical
                    : Orientation.Horizontal;
                break;
            case CarouselVirtualScrollLayout carouselLayout:
                // For carousel layouts, use StackLayout (container will handle viewport sizing)
                if (handler._layout is not WStackLayout carouselStackLayout)
                {
                    carouselStackLayout = new WStackLayout();
                    handler._layout = carouselStackLayout;
                    handler._itemsRepeater.Layout = carouselStackLayout;
                }
                
                carouselStackLayout.Orientation = carouselLayout.Orientation == ItemsLayoutOrientation.Vertical
                    ? Orientation.Vertical
                    : Orientation.Horizontal;
                break;
            default:
                throw new NotSupportedException($"Layout type {virtualScroll.ItemsLayout.GetType().Name} is not supported.");
        }
    }

    /// <summary>
    /// Maps the item template property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapItemTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._itemsSource?.NotifyReset();
        }
    }

    /// <summary>
    /// Maps the section header template property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapSectionHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._itemsSource?.NotifyReset();
        }
    }

    /// <summary>
    /// Maps the section footer template property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapSectionFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._itemsSource?.NotifyReset();
        }
    }

    /// <summary>
    /// Maps the header template property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapHeaderTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._itemsSource?.NotifyReset();
        }
    }

    /// <summary>
    /// Maps the footer template property from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapFooterTemplate(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (!handler.IsConnecting)
        {
            handler._itemsSource?.NotifyReset();
        }
    }

    /// <summary>
    /// Maps the refresh accent color property from the virtual scroll to the platform scroll viewer.
    /// </summary>
    public static void MapRefreshAccentColor(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Windows doesn't have a built-in refresh indicator like Android/iOS
        // This would need to be implemented with a custom control if needed
    }

    /// <summary>
    /// Maps the is refresh enabled property from the virtual scroll to the platform scroll viewer.
    /// </summary>
    public static void MapIsRefreshEnabled(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Windows doesn't have a built-in refresh indicator like Android/iOS
        // This would need to be implemented with a custom control if needed
    }

    /// <summary>
    /// Maps the is refreshing property from the virtual scroll to the platform scroll viewer.
    /// </summary>
    public static void MapIsRefreshing(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        if (handler._isUpdatingIsRefreshingFromPlatform)
        {
            return;
        }

        // Windows doesn't have a built-in refresh indicator like Android/iOS
        // This would need to be implemented with a custom control if needed
    }

    /// <summary>
    /// Maps the fading edge length property from the virtual scroll to the platform scroll viewer.
    /// </summary>
    public static void MapFadingEdgeLength(VirtualScrollHandler handler, IVirtualScroll virtualScroll)
    {
        // Windows doesn't support fading edges like Android
        // This is a no-op on Windows
    }

    /// <summary>
    /// Maps the ScrollTo command from the virtual scroll to the platform items repeater.
    /// </summary>
    public static void MapScrollTo(VirtualScrollHandler handler, IVirtualScroll virtualScroll, object? args)
    {
        if (args is not VirtualScrollCommandScrollToArgs scrollToArgs || handler._flattenedAdapter is null || handler._itemsRepeater is null)
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
        var flattenedIndex = handler._flattenedAdapter.GetFlattenedIndexForItem(sectionIndex, itemIndex);
        if (flattenedIndex < 0)
        {
            return;
        }

        // Get or create the element
        var element = handler._itemsRepeater.GetOrCreateElement(flattenedIndex);
        if (element is null)
        {
            return;
        }

        // Scroll to the element with proper positioning
        var bringIntoViewOptions = new BringIntoViewOptions { AnimationDesired = animated };
        
        // Adjust position based on ScrollToPosition
        // Windows uses alignment ratios: 0.0 = Start, 0.5 = Center, 1.0 = End
        var orientation = virtualScroll.ItemsLayout is LinearVirtualScrollLayout linearLayout
            ? linearLayout.Orientation
            : ItemsLayoutOrientation.Vertical;

        if (orientation == ItemsLayoutOrientation.Vertical)
        {
            bringIntoViewOptions.VerticalAlignmentRatio = position switch
            {
                ScrollToPosition.Start => 0.0,
                ScrollToPosition.Center => 0.5,
                ScrollToPosition.End => 1.0,
                _ => 0.0 // MakeVisible defaults to Start
            };
        }
        else
        {
            bringIntoViewOptions.HorizontalAlignmentRatio = position switch
            {
                ScrollToPosition.Start => 0.0,
                ScrollToPosition.Center => 0.5,
                ScrollToPosition.End => 1.0,
                _ => 0.0 // MakeVisible defaults to Start
            };
        }

        element.StartBringIntoView(bringIntoViewOptions);
    }

    /// <summary>
    /// Maps the scroll event enabled state from the virtual scroll to the platform scroll viewer.
    /// </summary>
    public static void MapSetScrollEventEnabled(VirtualScrollHandler handler, IVirtualScroll virtualScroll, object? args)
    {
        // Scroll events are always enabled on Windows via the property changed callbacks
        // This is a no-op but kept for API consistency
    }
}
