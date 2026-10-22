using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

internal class VirtualScrollRecyclerViewAdapter : Platform.VirtualScrollNativeAdapter
{
    private readonly VirtualScrollCellManager<VirtualScrollViewHolder> _cellManager = new(holder => holder.ViewWrapper.VirtualView);
    private readonly IMauiContext _mauiContext;
    private readonly IVirtualScroll _virtualScroll;
    private readonly IVirtualScrollFlattenedAdapter _adapter;
    private readonly VirtualScrollPlatformReuseIdManager _reuseIdManager;

    public VirtualScrollRecyclerViewAdapter(IMauiContext mauiContext, RecyclerView recyclerView, IVirtualScroll virtualScroll, IVirtualScrollFlattenedAdapter adapter)
    {
        _mauiContext = mauiContext;
        _virtualScroll = virtualScroll;
        _adapter = adapter;
        _reuseIdManager = new VirtualScrollPlatformReuseIdManager(recyclerView);

        HasStableIds = false;

        // The count is CACHED Java-side (getItemCount is the hottest adapter callback);
        // the notifier refreshes it on every changeset.
        UpdateItemCount(adapter.GetItemCount());
    }
    
    public override long GetItemId(int position) => RecyclerView.NoId;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _cellManager.Dispose();
            _adapter.Dispose();
        }
    }

    public override int GetItemViewType(int position)
    {
        var item = _adapter.GetItem(position);
        var template = item.Type switch
        {
            VirtualScrollFlattenedPositionType.Item => _virtualScroll.GetItemTemplate(item.Value),
            VirtualScrollFlattenedPositionType.SectionHeader => _virtualScroll.GetSectionHeaderTemplate(item.Value),
            VirtualScrollFlattenedPositionType.SectionFooter => _virtualScroll.GetSectionFooterTemplate(item.Value),
            VirtualScrollFlattenedPositionType.GlobalHeader => _virtualScroll.GetGlobalHeaderTemplate(),
            VirtualScrollFlattenedPositionType.GlobalFooter => _virtualScroll.GetGlobalFooterTemplate(),
            _ => throw new NotSupportedException($"Item type {item.Type} is not supported.")
        };

        var reuseId = template is null ? _reuseIdManager.DefaultReuseId : _reuseIdManager.GetReuseId(template, item.Type.ToString());

        return reuseId;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var item = _adapter.GetItem(position);

        if (holder is VirtualScrollViewHolder viewHolder)
        {
            // The cell's axes depend on the layout, and the layout can change while the pool still
            // holds cells built for the previous one — a vertical cell reused in a horizontal grid
            // would claim the whole viewport width and push every other cell off-screen. The
            // existing instance is mutated rather than replaced: RecyclerView casts it to its own
            // LayoutParams right after this call.
            var (cellWidth, cellHeight) = GetCellLayoutSize(_virtualScroll);

            if (viewHolder.ViewWrapper.LayoutParameters is { } layoutParameters
                && (layoutParameters.Width != cellWidth || layoutParameters.Height != cellHeight))
            {
                layoutParameters.Width = cellWidth;
                layoutParameters.Height = cellHeight;
            }

            // Only a grid stretches a cell beyond its content, and only the item cells: a header or
            // a footer is alone on its line, so there is never any slack to leave.
            viewHolder.ViewWrapper.ContentExtent = item.Type == VirtualScrollFlattenedPositionType.Item
                ? GetCellContentExtent(_virtualScroll)
                : VirtualScrollCellContentExtent.Fill;

            // The cell is about to show different content, so the size measured for the previous
            // item must not be reused.
            viewHolder.ViewWrapper.InvalidateMeasureCache();
        }

        if (holder is VirtualScrollViewHolder { ViewWrapper.VirtualView: BindableObject bindable })
        {
            if (item.Type is VirtualScrollFlattenedPositionType.GlobalFooter or VirtualScrollFlattenedPositionType.GlobalHeader)
            {
                bindable.ClearValue(BindableObject.BindingContextProperty);
            }
            else
            {
                bindable.BindingContext = item.Value;
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (_virtualScroll is Element virtualScrollElement && bindable is Element { Parent: null } viewElement)
            {
                virtualScrollElement.AddLogicalChild(viewElement);
            }
        }
    }

    // private readonly Dictionary<int, int> _countPerType = new();
    
    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        // ref var count = ref CollectionsMarshal.GetValueRefOrAddDefault(_countPerType, viewType, out _);
        // ++count;

        var template = _reuseIdManager.GetTemplateById(viewType);
        var view = (IView)template.CreateContent();

        var platformView = view.ToPlatform(_mauiContext);
        var recyclerView = (VirtualScrollRecyclerView) parent;
        var wrapperPlatformView = CreateViewHolderViewWrapper(recyclerView, _virtualScroll, view, platformView);
        var holder = new VirtualScrollViewHolder(wrapperPlatformView);
        
        _cellManager.TrackCell(holder);
        
        return holder;
    }

    private static VirtualScrollViewWrapper CreateViewHolderViewWrapper(VirtualScrollRecyclerView recyclerView, IVirtualScroll virtualScroll, IView view, AView platformView)
    {
        var wrapperPlatformView = new VirtualScrollViewWrapper(recyclerView.Context!);
        wrapperPlatformView.VirtualView = view;
        wrapperPlatformView.AddView(platformView);
        wrapperPlatformView.Id = AView.GenerateViewId();

        var (cellWidth, cellHeight) = GetCellLayoutSize(virtualScroll);
        wrapperPlatformView.LayoutParameters = new ViewGroup.LayoutParams(cellWidth, cellHeight);

        return wrapperPlatformView;
    }

    /// <summary>
    /// The layout params a cell must carry for the current items layout: it fills the cross axis
    /// and hugs its content along the scrolling one.
    /// </summary>
    /// <remarks>
    /// A grid uses the same values as a list — <see cref="GridLayoutManager" /> turns a cross-axis
    /// MatchParent into the span slot rather than the whole viewport.
    /// </remarks>
    /// <summary>
    /// Whether an item cell's content fills the cell or keeps its own extent along the scrolling axis.
    /// </summary>
    private static VirtualScrollCellContentExtent GetCellContentExtent(IVirtualScroll virtualScroll)
    {
        if (virtualScroll.ItemsLayout is not GridVirtualScrollLayout gridLayout)
        {
            return VirtualScrollCellContentExtent.Fill;
        }

        return gridLayout.Orientation == ItemsLayoutOrientation.Vertical
            ? VirtualScrollCellContentExtent.NaturalHeight
            : VirtualScrollCellContentExtent.NaturalWidth;
    }

    private static (int Width, int Height) GetCellLayoutSize(IVirtualScroll virtualScroll)
    {
        if (virtualScroll.ItemsLayout is CarouselVirtualScrollLayout)
        {
            return (ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        }

        return virtualScroll.ItemsLayout.Orientation == ItemsLayoutOrientation.Vertical
            ? (ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            : (ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.MatchParent);
    }
}
