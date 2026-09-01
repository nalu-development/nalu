using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Nalu;

/// <summary>
/// A non-generic, AOT-compatible adapter used by <see cref="VirtualScroll"/> to automatically wrap mutable
/// <see cref="INotifyCollectionChanged"/> lists while supporting drag and drop reordering.
/// </summary>
/// <remarks>
/// When the wrapped collection derives from <see cref="ObservableCollection{T}"/>, reordering uses its
/// <c>Move</c> method (resolved through metadata-only reflection) so other subscribers of the collection
/// observe a single Move notification; otherwise it falls back to <see cref="IList.RemoveAt"/> +
/// <see cref="IList.Insert"/>. Either way the notifications raised during the drag are ignored by this
/// adapter's own subscription, as the platform has already moved the dragged element.
/// </remarks>
internal sealed class VirtualScrollReorderableNotifyCollectionChangedAdapter : VirtualScrollNotifyCollectionChangedAdapter, IReorderableVirtualScrollAdapter
{
    private MethodInfo? _observableCollectionMoveMethod;
    private bool _moveMethodResolved;
    private bool _movingItemsViaDrag;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollReorderableNotifyCollectionChangedAdapter"/> class.
    /// </summary>
    /// <param name="collection">A mutable list which also implements <see cref="INotifyCollectionChanged"/>.</param>
    public VirtualScrollReorderableNotifyCollectionChangedAdapter(IList collection)
        : base(collection)
    {
    }

    /// <inheritdoc/>
    public bool CanDragItem(VirtualScrollDragInfo dragInfo) => true;

    /// <inheritdoc/>
    void IVirtualScrollDragHandler.MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
    {
        _movingItemsViaDrag = true;

        try
        {
            MoveItem(dragMoveInfo);
        }
        finally
        {
            _movingItemsViaDrag = false;
        }
    }

    /// <inheritdoc/>
    public bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo) => true;

    /// <inheritdoc/>
    public void OnDragStarted(VirtualScrollDragInfo dragInfo)
    {
    }

    /// <inheritdoc/>
    public void OnDragInitiating(VirtualScrollDragInfo dragInfo)
    {
    }

    /// <inheritdoc/>
    public void OnDragEnded(VirtualScrollDragInfo dragInfo)
    {
    }

    /// <inheritdoc/>
    private protected override bool ShouldIgnoreCollectionChanges() => _movingItemsViaDrag;

    /// <inheritdoc cref="IVirtualScrollDragHandler.MoveItem"/>
    private void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
    {
        var fromIndex = dragMoveInfo.CurrentItemIndex;
        var toIndex = dragMoveInfo.DestinationItemIndex;

        if (!_moveMethodResolved)
        {
            _observableCollectionMoveMethod = ResolveObservableCollectionMoveMethod(Collection.GetType());
            _moveMethodResolved = true;
        }

        if (_observableCollectionMoveMethod is { } moveMethod)
        {
            moveMethod.Invoke(Collection, [fromIndex, toIndex]);
            return;
        }

        var item = Collection[fromIndex];
        Collection.RemoveAt(fromIndex);
        Collection.Insert(toIndex, item);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Best-effort lookup: when trimming removed ObservableCollection<T>.Move, GetMethod returns null and the IList RemoveAt+Insert fallback is used.")]
    private static MethodInfo? ResolveObservableCollectionMoveMethod(Type? type)
    {
        while (type is not null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ObservableCollection<>))
            {
                return type.GetMethod(nameof(ObservableCollection<object>.Move), BindingFlags.Public | BindingFlags.Instance, [typeof(int), typeof(int)]);
            }

            type = type.BaseType;
        }

        return null;
    }
}
