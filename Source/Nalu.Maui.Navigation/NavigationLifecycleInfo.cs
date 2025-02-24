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
    /// Gets the requested state of the navigation.
    /// </summary>
    public string RequestedState { get; }

    /// <summary>
    /// Gets the current state of the navigation.
    /// </summary>
    public string CurrentState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationLifecycleInfo" /> class.
    /// </summary>
    /// <param name="navigation">The requested navigation object.</param>
    /// <param name="requestedState">The requested navigation state.</param>
    /// <param name="currentState">The current navigation state.</param>
    public NavigationLifecycleInfo(INavigationInfo navigation, string requestedState, string currentState)
    {
        Navigation = navigation;
        RequestedState = requestedState;
        CurrentState = currentState;
    }
}
