using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Size = Windows.Foundation.Size;

namespace Nalu;

/// <summary>
/// Container for virtualized elements in Windows ItemsRepeater.
/// </summary>
internal partial class VirtualScrollElementContainer : ContentControl
{
    private static WeakReference<VirtualScrollPlatformView>? _cachedScrollViewer;

    public VirtualScrollElementContainer(IMauiContext context, string reuseId, IVirtualScroll virtualScroll)
        : base()
    {
        MauiContext = context;
        ReuseId = reuseId;
        VirtualScroll = virtualScroll;

        _measureForCarousel = virtualScroll.ItemsLayout is CarouselVirtualScrollLayout;
        HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
        VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
        VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch;
    }

    public readonly string ReuseId;
    public readonly IMauiContext MauiContext;
    public readonly IVirtualScroll VirtualScroll;

    private readonly bool _measureForCarousel; 

    public VirtualScrollFlattenedItem? FlattenedItem { get; private set; }
    public int FlattenedIndex { get; internal set; } = -1;
    internal bool IsRecycled { get; set; }

    public bool NeedsView => VirtualView is null;

    public IView? VirtualView { get; private set; }

    public void SetupView(IView view)
    {
        if (VirtualView is not null)
        {
            throw new InvalidOperationException("VirtualScrollElementContainer is already initialized.");
        }

        Content = view.ToPlatform(MauiContext);
        VirtualView = view;
    }

    public void UpdateItem(VirtualScrollFlattenedItem item, int flattenedIndex)
    {
        FlattenedItem = item;
        FlattenedIndex = flattenedIndex;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        // For carousel layouts, constrain item size to viewport size in the scroll direction
        if (_measureForCarousel)
        {
            var scrollViewer = GetScrollViewer();
            if (scrollViewer is not null)
            {
                measured = scrollViewer.LastMeasureConstraint;
            }
        }
        
        return measured;
    }

    private VirtualScrollPlatformView? GetScrollViewer()
    {
        // Try to get from cache first
        if (_cachedScrollViewer is not null && _cachedScrollViewer.TryGetTarget(out var cached))
        {
            return cached;
        }

        // Find ScrollViewer in visual tree and cache it
        var scrollViewer = FindAncestor<VirtualScrollPlatformView>(this);
        if (scrollViewer is not null)
        {
            _cachedScrollViewer = new WeakReference<VirtualScrollPlatformView>(scrollViewer);
        }

        return scrollViewer;
    }

    private static T? FindAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T match)
            {
                return match;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    protected override IEnumerable<DependencyObject> GetChildrenInTabFocusOrder()
    {
        if (IsRecycled)
        {
            return Enumerable.Empty<DependencyObject>();
        }
        else
        {
            return base.GetChildrenInTabFocusOrder();
        }
    }
}
