using AndroidX.RecyclerView.Widget;

namespace Nalu;

internal class VirtualScrollTouchHelperCallback : ItemTouchHelper.Callback
{
    private IVirtualScrollFlattenedAdapter? _flattenedAdapter;
    private IVirtualScroll? _virtualScroll;
    private int _originalSectionIndex = -1;
    private int _originalItemIndex = -1;
    private WeakReference<object?>? _draggingItem;

    public override bool IsLongPressDragEnabled => true;

    public VirtualScrollTouchHelperCallback(IVirtualScroll virtualScroll)
    {
        _virtualScroll = virtualScroll;
    }

    protected override void Dispose(bool disposing)
    {
        _virtualScroll = null;
        _flattenedAdapter = null;
        _draggingItem = null;
        base.Dispose(disposing);
    }

    public override bool CanDropOver(RecyclerView recyclerView, RecyclerView.ViewHolder current, RecyclerView.ViewHolder target)
    {
        if (_flattenedAdapter is null ||
            _virtualScroll?.DragHandler is not { } dragHandler ||
            _virtualScroll.Adapter is not { } adapter ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(current.AbsoluteAdapterPosition, out var sourceSectionIndex, out var sourceItemIndex) ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(target.AbsoluteAdapterPosition, out var destinationSectionIndex, out var destinationItemIndex))
        {
            return false;
        }
        
        var dragMoveInfo = new VirtualScrollDragDropInfo(
            GetItemWithCache(adapter, sourceSectionIndex, sourceItemIndex),
            _originalSectionIndex,
            _originalItemIndex,
            sourceSectionIndex,
            sourceItemIndex,
            destinationSectionIndex,
            destinationItemIndex
        );

        dragHandler.CanDropItemAt(dragMoveInfo);
        
        return true;
    }

    private object? GetItemWithCache(IVirtualScrollAdapter adapter, int sourceSectionIndex, int sourceItemIndex) => _draggingItem?.TryGetTarget(out var cachedItem) is true ? cachedItem : adapter.GetItem(sourceSectionIndex, sourceItemIndex);

    public override int GetMovementFlags(RecyclerView recyclerView, RecyclerView.ViewHolder source)
    {
        if (_flattenedAdapter is null || 
            _virtualScroll?.DragHandler is not { } dragHandler ||
            _virtualScroll.Adapter is not { } adapter ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(source.AbsoluteAdapterPosition, out var sectionIndex, out var itemIndex))
        {
            return MakeMovementFlags(0, 0);
        }

        var dragInfo = new VirtualScrollDragInfo(adapter.GetItem(sectionIndex, itemIndex), sectionIndex, itemIndex);
        if (!dragHandler.CanDragItem(dragInfo))
        {
            return MakeMovementFlags(0, 0);
        }
        
        var orientation = ((VirtualScrollRecyclerView) recyclerView).Orientation;
        var dragFlags = orientation == ItemsLayoutOrientation.Vertical ? ItemTouchHelper.Up | ItemTouchHelper.Down : ItemTouchHelper.Left | ItemTouchHelper.Right;

        _originalSectionIndex = dragInfo.SectionIndex;
        _originalItemIndex = dragInfo.ItemIndex;
        _draggingItem = new WeakReference<object?>(dragInfo.Item);
        dragHandler.OnDragStarted(dragInfo);

        return MakeMovementFlags(dragFlags, 0);
    }

    public override bool OnMove(RecyclerView recyclerView, RecyclerView.ViewHolder current, RecyclerView.ViewHolder target)
    {
        if (_flattenedAdapter is null ||
            _virtualScroll?.DragHandler is not { } dragHandler ||
            _virtualScroll.Adapter is not { } adapter ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(current.AbsoluteAdapterPosition, out var sourceSectionIndex, out var sourceItemIndex) ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(target.AbsoluteAdapterPosition, out var destinationSectionIndex, out var destinationItemIndex))
        {
            return false;
        }
        
        var dragMoveInfo = new VirtualScrollDragMoveInfo(
            GetItemWithCache(adapter, sourceSectionIndex, sourceItemIndex),
            sourceSectionIndex,
            sourceItemIndex,
            destinationSectionIndex,
            destinationItemIndex
        );

        dragHandler.MoveItem(dragMoveInfo);

        // Android wants us to do the move
        _flattenedAdapter.OnAdapterChanged(
            new VirtualScrollChangeSet(
                [
                    new VirtualScrollChange(
                        VirtualScrollChangeOperation.MoveItem,
                        sourceSectionIndex,
                        sourceItemIndex,
                        destinationSectionIndex,
                        destinationItemIndex
                    )
                ]
            )
        );
        
        return true;
    }

    public override void OnSwiped(RecyclerView.ViewHolder p0, int p1)
    {
    }

    public override void ClearView(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
    {
        if (_virtualScroll?.DragHandler is { } dragHandler &&
            _flattenedAdapter is not null &&
            _virtualScroll.Adapter is { } adapter &&
            _flattenedAdapter.TryGetSectionAndItemIndex(viewHolder.AbsoluteAdapterPosition, out var sectionIndex, out var itemIndex))
        {
            var dragInfo = new VirtualScrollDragInfo(GetItemWithCache(adapter, sectionIndex, itemIndex), sectionIndex, itemIndex);
            dragHandler.OnDragEnded(dragInfo);
        }

        _originalSectionIndex = -1;
        _originalItemIndex = -1;
        _draggingItem = null;
    }

    public void SetAdapter(IVirtualScrollFlattenedAdapter? adapter) => _flattenedAdapter = adapter;
}
