#if IOS || MACCATALYST || ANDROID || WINDOWS
using Microsoft.Maui.Handlers;

#if IOS || MACCATALYST
using PlatformView = UIKit.UIView;
#elif ANDROID
using PlatformView = Android.Views.View;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.FrameworkElement;
#endif

namespace Nalu;


/// <summary>
/// Handler for the <see cref="VirtualScroll" /> view.
/// </summary>
public partial class VirtualScrollHandler : ViewHandler<IVirtualScroll, PlatformView>
{
    /// <summary>
    /// The property mapper for the <see cref="IVirtualScroll" /> interface.
    /// </summary>
    public static readonly IPropertyMapper<IVirtualScroll, VirtualScrollHandler> Mapper =
        new PropertyMapper<IVirtualScroll, VirtualScrollHandler>(ViewMapper)
        {
            [nameof(IVirtualScroll.Adapter)] = MapAdapter,
            [nameof(IVirtualScroll.ItemsLayout)] = MapLayout,
            [nameof(IVirtualScroll.ItemTemplate)] = MapItemTemplate,
            [nameof(IVirtualScroll.SectionHeaderTemplate)] = MapSectionHeaderTemplate,
            [nameof(IVirtualScroll.SectionFooterTemplate)] = MapSectionFooterTemplate,
            [nameof(IVirtualScroll.HeaderTemplate)] = MapHeaderTemplate,
            [nameof(IVirtualScroll.FooterTemplate)] = MapFooterTemplate,
            [nameof(IVirtualScroll.IsRefreshEnabled)] = MapIsRefreshEnabled,
            [nameof(IVirtualScroll.RefreshAccentColor)] = MapRefreshAccentColor,
            [nameof(IVirtualScroll.IsRefreshing)] = MapIsRefreshing,
            [nameof(IVirtualScroll.DragHandler)] = MapDragHandler,
            [nameof(VirtualScroll.FadingEdgeLength)] = MapFadingEdgeLength,
#if IOS || MACCATALYST || ANDROID
            [nameof(IVirtualScroll.SizingStrategy)] = MapSizingStrategy,
#endif
#if IOS || MACCATALYST
            [nameof(IVirtualScroll.Background)] = MapBackground,
#endif
        };

    /// <summary>
    /// The command mapper for the <see cref="IVirtualScroll" /> interface.
    /// </summary>
    public static readonly CommandMapper<IVirtualScroll, VirtualScrollHandler> CommandMapper =
        new(ViewCommandMapper)
        {
            [nameof(IVirtualScroll.ScrollTo)] = MapScrollTo,
            ["SetScrollEventEnabled"] = MapSetScrollEventEnabled,
        };

    /// <summary>
    /// A flag to skip the layout mapper during initial setup.
    /// </summary>
    protected bool IsConnecting { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollHandler" /> class.
    /// </summary>
    public VirtualScrollHandler()
        : base(Mapper, CommandMapper)
    {
    }

    /// <inheritdoc />
    public override void SetVirtualView(IView view)
    {
        IsConnecting = true;
        base.SetVirtualView(view);
        IsConnecting = false;
    }

#if IOS || MACCATALYST || ANDROID
    /// <summary>
    /// Extent changes below this (device-independent units) never reach the layout system.
    /// </summary>
    private const double _sizeToContentEpsilon = 0.5;

    /// <summary>
    /// Applies the strategy to a measured content extent: capped by <see cref="VirtualScrollSizingMode.Max" />
    /// and, either way, never larger than what the parent offered.
    /// </summary>
    private static double ClampExtent(double contentExtent, VirtualScrollSizingStrategy strategy, double constraint)
    {
        var extent = Math.Max(0, contentExtent);

        if (strategy.Mode == VirtualScrollSizingMode.Max)
        {
            extent = Math.Min(extent, strategy.MaxExtent);
        }

        if (!double.IsInfinity(constraint) && !double.IsNaN(constraint))
        {
            extent = Math.Min(extent, constraint);
        }

        return extent;
    }

#if DEBUG
    private bool _unboundedWarningIssued;

    /// <summary>
    /// DEBUG-only nudge away from the expensive mode: <see cref="VirtualScrollSizingMode.Unbounded" />
    /// lays out the WHOLE collection and re-measures on every content change, which stops being
    /// reasonable well before a feed-sized list. Warns once per handler.
    /// </summary>
    private void WarnIfUnboundedIsExpensive(IVirtualScroll virtualScroll)
    {
        const int itemThreshold = 200;

        if (_unboundedWarningIssued
            || virtualScroll.SizingStrategy.Mode != VirtualScrollSizingMode.Unbounded
            || virtualScroll.Adapter is not { } adapter)
        {
            return;
        }

        var sectionCount = adapter.GetSectionCount();
        var itemCount = 0;

        // Stops counting as soon as the threshold is passed: the count itself must stay cheap.
        for (var section = 0; section < sectionCount && itemCount <= itemThreshold; section++)
        {
            itemCount += adapter.GetItemCount(section);
        }

        if (itemCount > itemThreshold)
        {
            _unboundedWarningIssued = true;

            Console.WriteLine(
                $"[Nalu.VirtualScroll] SizingStrategy=Unbounded with more than {itemThreshold} items: the whole collection is laid out and re-measured on every content change. " +
                "Prefer VirtualScrollSizingStrategy.Max(extent), whose cost is bounded by the items that fit within the cap."
            );
        }
    }
#endif

    /// <summary>
    /// A first-pass guess at the content extent, from the layout's own size estimates. Used only
    /// to break the chicken-and-egg of the very first measure (a platform scroller reports no
    /// content size until it has been laid out, and it is never laid out while it measures zero);
    /// the real extent replaces it as soon as the content settles.
    /// </summary>
    // ReSharper disable once UnusedMember.Local — used by the Apple platform partial
    private static double EstimateContentExtent(IVirtualScroll virtualScroll)
    {
        if (virtualScroll.ItemsLayout is not LinearVirtualScrollLayout linearLayout || virtualScroll.Adapter is not { } adapter)
        {
            return 0;
        }

        var sectionCount = adapter.GetSectionCount();

        // A grid stacks lines, not items: counting items would overestimate the extent Span-fold.
        // Lines are counted per section because a section never shares a line with another.
        var span = linearLayout is GridVirtualScrollLayout gridLayout ? gridLayout.Span : 1;
        var lineCount = 0;

        for (var section = 0; section < sectionCount; section++)
        {
            lineCount += (adapter.GetItemCount(section) + span - 1) / span;
        }

        var extent = lineCount * linearLayout.EstimatedItemSize;

        if (virtualScroll.HeaderTemplate is not null)
        {
            extent += linearLayout.EstimatedHeaderSize;
        }

        if (virtualScroll.FooterTemplate is not null)
        {
            extent += linearLayout.EstimatedFooterSize;
        }

        return extent;
    }

    /// <summary>Whether the adapter currently reports at least one item, header or footer.</summary>
    // ReSharper disable once UnusedMember.Local — used by the Apple platform partial
    private static bool HasContent(IVirtualScroll virtualScroll)
    {
        if (virtualScroll.Adapter is not { } adapter)
        {
            return false;
        }

        var sectionCount = adapter.GetSectionCount();

        for (var section = 0; section < sectionCount; section++)
        {
            if (adapter.GetItemCount(section) > 0)
            {
                return true;
            }
        }

        return sectionCount > 0 || virtualScroll.HeaderTemplate is not null || virtualScroll.FooterTemplate is not null;
    }
#endif

    private void EnsureCreatedCellsCleanup()
    {
        if (VirtualView is Element element)
        {
            static void ElementOnChildRemoved(object? sender, ElementEventArgs e)
            {
                var element = e.Element;
                (element as View)?.DisconnectHandlers();
                element.BindingContext = null;
            }

            element.ChildRemoved += ElementOnChildRemoved;
            element.ClearLogicalChildren();
            element.ChildRemoved -= ElementOnChildRemoved;
        }
    }
}
#endif
