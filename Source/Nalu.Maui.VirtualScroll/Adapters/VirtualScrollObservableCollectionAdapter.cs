using System.Collections.ObjectModel;

namespace Nalu;

/// <summary>
/// An adapter that wraps an observable collection for use with <see cref="VirtualScroll"/> supporting drag and drop.
/// </summary>
/// <typeparam name="TItem"></typeparam>
public class VirtualScrollObservableCollectionAdapter<TItem> : VirtualScrollNotifyCollectionChangedAdapter<ObservableCollection<TItem>>, IReorderableVirtualScrollAdapter
{
    private bool _movingItemsViaDrag;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollObservableCollectionAdapter{TItem}"/> class.
    /// </summary>
    /// <param name="collection"></param>
    public VirtualScrollObservableCollectionAdapter(ObservableCollection<TItem> collection) : base(collection)
    {
    }
    
    /// <inheritdoc/>
    public virtual bool CanDragItem(VirtualScrollDragInfo dragInfo) => true;

    /// <inheritdoc/>
    public virtual void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
    {
        _movingItemsViaDrag = true;

        try
        {
            Collection.Move(dragMoveInfo.CurrentItemIndex, dragMoveInfo.DestinationItemIndex);
        }
        finally
        {
            _movingItemsViaDrag = false;
        }
    }

    /// <inheritdoc/>
    public virtual bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo) => true;

    /// <inheritdoc/>
    public virtual void OnDragStarted(VirtualScrollDragInfo dragInfo)
    {
    }
    
    /// <inheritdoc/>
    public virtual void OnDragInitiating(VirtualScrollDragInfo dragInfo)
    {
    }
    
    /// <inheritdoc/>
    public virtual void OnDragEnded(VirtualScrollDragInfo dragInfo)
    {
    }

    /// <inheritdoc/>
    protected override bool ShouldIgnoreCollectionChanges() => _movingItemsViaDrag;
}
