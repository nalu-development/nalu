namespace Nalu;

/// <summary>
/// Defines how the lifecycle event has been handled.
/// </summary>
[Flags]
public enum NavigationLifecycleHandling
{
    /// <summary>
    /// The event has not been handled.
    /// </summary>
    NotHandled = 0,

    /// <summary>
    /// The event has been handled.
    /// </summary>
    Handled = 1,

    /// <summary>
    /// The event has been handled with an intent.
    /// </summary>
    HandledWithIntent = Handled | 2
}
