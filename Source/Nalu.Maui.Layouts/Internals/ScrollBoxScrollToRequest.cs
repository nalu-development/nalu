namespace Nalu.Internals;

/// <summary>
/// A programmatic scroll request travelling from <see cref="ScrollBox.ScrollToAsync(double, double, bool)" />
/// to the platform handler through the command mapper.
/// </summary>
/// <remarks>
/// The completion contract implemented by every platform:
/// <list type="bullet">
/// <item>requests issued before the first layout pass are queued (the latest pending request wins)
/// and executed right after the first layout;</item>
/// <item>the task ALWAYS completes — including no-op targets and requests superseded by a newer
/// one — so <c>await ScrollToAsync(...)</c> can never hang;</item>
/// <item>targets are clamped against the platform's adjusted content insets.</item>
/// </list>
/// </remarks>
internal sealed class ScrollBoxScrollToRequest(double x, double y, bool animated)
{
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Target horizontal distance from the start of the content, in device-independent units.</summary>
    /// <remarks>Settable: descendant-targeting requests resolve their coordinates lazily, on the first layout pass.</remarks>
    public double X { get; set; } = x;

    /// <summary>Target vertical distance from the start of the content, in device-independent units.</summary>
    /// <remarks>Settable: descendant-targeting requests resolve their coordinates lazily, on the first layout pass.</remarks>
    public double Y { get; set; } = y;

    /// <summary>Whether the scroll should animate.</summary>
    public bool Animated { get; } = animated;

    /// <summary>The task completed when the scroll settles (or the request is superseded).</summary>
    public Task Task => _completionSource.Task;

    /// <summary>Marks the request as done. Idempotent.</summary>
    public void Complete() => _completionSource.TrySetResult();
}
