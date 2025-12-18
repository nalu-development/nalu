namespace Nalu;

/// <summary>
/// Defines a set of changes in the virtual scroll.
/// </summary>
/// <param name="Changes"></param>
public sealed record VirtualScrollChangeSet(IEnumerable<VirtualScrollChange> Changes);
