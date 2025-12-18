namespace Nalu;

/// <summary>
/// Describes a change in the virtual scroll data.
/// </summary>
public sealed class VirtualScrollChange
{
    /// <summary>
    /// Gets the operation that caused the change.
    /// </summary>
    public VirtualScrollChangeOperation Operation { get; }

    /// <summary>
    /// Gets a value indicating whether the change is at the section level.
    /// </summary>
    public bool IsSectionChange => Operation >= VirtualScrollChangeOperation.InsertSection;

    /// <summary>
    /// Gets the item index affected by the change.
    /// </summary>
    public int StartItemIndex { get; }
    
    /// <summary>
    /// Gets the end item index affected by the change.
    /// </summary>
    public int EndItemIndex { get; }

    /// <summary>
    /// Gets the section index affected by the change.
    /// </summary>
    public int StartSectionIndex { get; }
    
    /// <summary>
    /// Gets the end section index affected by the change.
    /// </summary>
    public int EndSectionIndex { get; }

    internal VirtualScrollChange(VirtualScrollChangeOperation operation, int startSectionIndex, int startItemIndex, int endSectionIndex, int endItemIndex)
    {
        Operation = operation;
        StartItemIndex = startItemIndex;
        EndItemIndex = endItemIndex;
        StartSectionIndex = startSectionIndex;
        EndSectionIndex = endSectionIndex;
    }
}
