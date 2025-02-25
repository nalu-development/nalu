namespace Nalu;

/// <summary>
/// Holds information about a navigation lifecycle event.
/// </summary>
public class NavigationLifecycleInfo
{
    /// <summary>
    /// Gets the requested navigation object.
    /// </summary>
    public INavigationInfo Navigation { get; }

    /// <summary>
    /// Gets the requested navigation.
    /// </summary>
    public string RequestedNavigation { get; }

    /// <summary>
    /// Gets the target state of the navigation.
    /// </summary>
    public string TargetState { get; }

    /// <summary>
    /// Gets the current state of the navigation.
    /// </summary>
    public string CurrentState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationLifecycleInfo" /> class.
    /// </summary>
    /// <param name="navigation">The requested navigation object.</param>
    /// <param name="requestedNavigation">The requested navigation.</param>
    /// <param name="targetState">The target navigation state.</param>
    /// <param name="currentState">The current navigation state.</param>
    public NavigationLifecycleInfo(INavigationInfo navigation, string requestedNavigation, string targetState, string currentState)
    {
        Navigation = navigation;
        RequestedNavigation = requestedNavigation;
        TargetState = targetState;
        CurrentState = currentState;
    }
}
