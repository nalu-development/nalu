namespace Nalu;

/// <summary>
/// Base class for flattened (non-sectioned) item-level changes in the virtual scroll data.
/// </summary>
internal class VirtualScrollFlattenedChange
{
    /// <summary>
    /// Gets the operation that caused the change.
    /// </summary>
    public VirtualScrollFlattenedChangeOperation Operation { get; }

    /// <summary>
    /// Gets the item index affected by the change.
    /// </summary>
    public int StartItemIndex { get; }
    
    /// <summary>
    /// Gets the end item index affected by the change.
    /// </summary>
    public int EndItemIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollFlattenedChange"/> class.
    /// </summary>
    /// <param name="operation">The operation that caused the change.</param>
    /// <param name="startItemIndex">The starting item index affected by the change.</param>
    /// <param name="endItemIndex">The ending item index affected by the change.</param>
    internal VirtualScrollFlattenedChange(VirtualScrollFlattenedChangeOperation operation, int startItemIndex, int endItemIndex)
    {
        Operation = operation;
        StartItemIndex = startItemIndex;
        EndItemIndex = endItemIndex;
    }
}

