using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    private WStackLayout? _layout;
    private WGrid? _rootLayout;
    private IVirtualScrollFlattenedAdapter? _flattenedAdapter;
    private bool _isUpdatingIsRefreshingFromPlatform;
    private VirtualScrollDragDropHelper? _dragDropHelper;

    /// <inheritdoc />
    protected override PlatformView CreatePlatformView()
    {
        _rootLayout = new WGrid();

        _itemsRepeaterScrollHost = new ItemsRepeaterScrollHost();
        _scrollViewer = new ScrollViewer();
        _itemsRepeater = new ItemsRepeater();

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

        controller.Scrolled(scrollX, scrollY, totalWidth, totalHeight);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(PlatformView platformView)
    {
        _notifier?.Dispose();
        _notifier = null;

        _elementFactory?.Dispose();
        _elementFactory = null;

        _itemsRepeater!.ItemTemplate = null;
        _itemsRepeater.ItemsSource = null;
        _itemsSource = null;
        _flattenedAdapter = null;
        _reuseIdManager = null;
        _dragDropHelper?.Dispose();
        _dragDropHelper = null;

        if (_itemsRepeaterScrollHost is not null)
        {
            _itemsRepeaterScrollHost.Loaded -= ItemsRepeaterScrollHost_Loaded;
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
        if (_itemsRepeater is null || _flattenedAdapter is null)
        {
            return null;
        }

        var childrenCount = VisualTreeHelper.GetChildrenCount(_itemsRepeater);
        if (childrenCount == 0)
        {
            return null;
        }

        (int Section, int Item)? start = null;
        (int Section, int Item)? end = null;

        for (var i = 0; i < childrenCount; i++)
        {
            if (VisualTreeHelper.GetChild(_itemsRepeater, i) is VirtualScrollElementContainer container)
            {
                if (!IsElementVisible(container, _itemsRepeater))
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
        }

        if (!start.HasValue || !end.HasValue)
        {
            return null;
        }

        return new VirtualScrollRange(start.Value.Section, start.Value.Item, end.Value.Section, end.Value.Item);
    }

    private static bool IsElementVisible(VirtualScrollElementContainer element, FrameworkElement container)
    {
        try
        {
            var bounds = element.TransformToVisual(container).TransformBounds(new WRect(0.0, 0.0, element.ActualWidth, element.ActualHeight));

            return bounds.Left < container.ActualWidth
                && bounds.Top < container.ActualHeight
                && bounds.Right > 0
                && bounds.Bottom > 0;
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
            handler._elementFactory = new VirtualScrollElementFactory(mauiContext, virtualScroll, flattenedAdapter, handler._reuseIdManager);
            itemsRepeater.ItemTemplate = handler._elementFactory;

            handler._itemsSource = new VirtualScrollItemsSource(flattenedAdapter);
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
            handler._itemsSource = null;
            handler._flattenedAdapter = null;
            handler._reuseIdManager = null;
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
        if (handler._layout is null)
        {
            return;
        }

        switch (virtualScroll.ItemsLayout)
        {
            case LinearVirtualScrollLayout linearLayout:
                handler._layout.Orientation = linearLayout.Orientation == ItemsLayoutOrientation.Vertical
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
