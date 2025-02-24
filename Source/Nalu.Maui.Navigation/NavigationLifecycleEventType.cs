namespace Nalu;

/// <summary>
/// The type of lifecycle event.
/// </summary>
public enum NavigationLifecycleEventType
{
    /// <summary>
    /// Triggered when a navigation request starts.
    /// </summary>
    NavigationRequested,
    /// <summary>
    /// Triggered when a navigation request is completed.
    /// </summary>
    NavigationCompleted,
    /// <summary>
    /// Triggered when a navigation request fails.
    /// </summary>
    NavigationFailed,
    /// <summary>
    /// Triggered when a page is entering the navigation stack.
    /// </summary>
    Entering,
    /// <summary>
    /// Triggered when a page is leaving the navigation stack.
    /// </summary>
    Leaving,
    /// <summary>
    /// Triggered when a page is appearing on the screen.
    /// </summary>
    Appearing,
    /// <summary>
    /// Triggered when a page is disappearing from the screen.
    /// </summary>
    Disappearing,
    /// <summary>
    /// Triggered when a page can leave the navigation stack.
    /// </summary>
    LeavingGuard,
}
