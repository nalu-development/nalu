namespace Nalu;

/// <summary>
/// A <see cref="VirtualScroll.ItemsSource"/> adapter that provides support for batching updates notifications.
/// </summary>
/// <remarks>
/// This is especially useful for scenarios where multiple changes need to be made to the data source
/// allowing the adapter to optimize notifications and UI updates avoiding potential glitches or performance issues.
/// </remarks>
public interface IVirtualScrollBatchableSource : IVirtualScrollAdapter
{
    /// <summary>
    /// Performs a batch update of the data source.
    /// </summary>
    /// <remarks>
    /// All notifications generated during the execution of the <paramref name="updateAction"/> will be
    /// consolidated into a single notification to optimize performance and stability.
    /// </remarks>
    /// <param name="updateAction">The action that performs the updates.</param>
    void PerformBatchUpdates(Action updateAction);
}
