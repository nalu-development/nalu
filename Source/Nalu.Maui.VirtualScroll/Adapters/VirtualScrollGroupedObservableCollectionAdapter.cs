using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Nalu;

/// <summary>
/// 
/// </summary>
/// <typeparam name="TSectionCollection"></typeparam>
/// <typeparam name="TItem"></typeparam>
public class VirtualScrollGroupedObservableCollectionAdapter<TSectionCollection, TItem> : VirtualScrollGroupedNotifyCollectionChangedAdapter<TSectionCollection, ObservableCollection<TItem>>, IVirtualScrollSource
    where TSectionCollection : IList, INotifyCollectionChanged
{
    private bool _movingItemsViaDrag;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollGroupedObservableCollectionAdapter{TSectionCollection,TItem}"/> class.
    /// </summary>
    /// <param name="sections">The collection of sections.</param>
    /// <param name="sectionItemsGetter">A function that extracts the items collection from a section object.</param>
    public VirtualScrollGroupedObservableCollectionAdapter(TSectionCollection sections, Func<object, ObservableCollection<TItem>> sectionItemsGetter) : base(sections, sectionItemsGetter)
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
            var currentSectionIndex = dragMoveInfo.CurrentSectionIndex;
            var destinationSectionIndex = dragMoveInfo.DestinationSectionIndex;

            var source = GetSectionItems(currentSectionIndex);

            if (currentSectionIndex == destinationSectionIndex)
            {
                source.Move(dragMoveInfo.CurrentItemIndex, dragMoveInfo.DestinationItemIndex);
            }
            else
            {
                var destination = GetSectionItems(dragMoveInfo.DestinationSectionIndex);
                source.RemoveAt(dragMoveInfo.CurrentItemIndex);
                destination.Insert(dragMoveInfo.DestinationItemIndex, (TItem) dragMoveInfo.Item!);
            }
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
