namespace Nalu;

/// <summary>
/// Defines a set of changes in the virtual scroll.
/// </summary>
/// <param name="Changes"></param>
internal sealed record VirtualScrollFlattenedChangeSet(IEnumerable<VirtualScrollFlattenedChange> Changes);
