namespace Nalu;

/// <summary>
/// Event arguments for Nalu navigation lifecycle events.
/// </summary>
public class NavigationLifecycleEventArgs : EventArgs
{
    /// <summary>
    /// Gets the type of lifecycle event.
    /// </summary>
    public NavigationLifecycleEventType EventType { get; }

    /// <summary>
    /// Gets the target of the lifecycle event.
    /// </summary>
    /// <remarks>
    /// Contains an instance of <see cref="INavigationInfo"/> when the event type is <see cref="NavigationLifecycleEventType.NavigationRequested"/>
    /// or <see cref="NavigationLifecycleEventType.NavigationCompleted"/> or <see cref="NavigationLifecycleEventType.NavigationFailed"/>.
    /// In all other cases, it contains the instance of the <see cref="Page"/>'s <see cref="BindableObject.BindingContext"/> on which the event is applied.
    /// </remarks>
    public object Target { get; }

    /// <summary>
    /// Gets the additional information if any.
    /// </summary>
    /// <remarks>
    /// Contains <see cref="NavigationLifecycleInfo"/> object when the event type is <see cref="NavigationLifecycleEventType.NavigationRequested"/>
    /// or <see cref="NavigationLifecycleEventType.NavigationCompleted"/> or <see cref="NavigationLifecycleEventType.NavigationFailed"/>.
    /// In all other cases, it contains the intent applied (if any).
    /// </remarks>
    public object? Data { get; }

    /// <summary>
    /// Gets a value indicating whether the event has been handled by the target.
    /// </summary>
    public NavigationLifecycleHandling Handling { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationLifecycleEventArgs" /> class.
    /// </summary>
    /// <param name="eventType">The lifecycle event type.</param>
    /// <param name="target">The target object on which the lifecycle is being executed.</param>
    /// <param name="handling">A value indicating whether the event has been handled by the target.</param>
    /// <param name="data">Additional information.</param>
    public NavigationLifecycleEventArgs(NavigationLifecycleEventType eventType, object target, NavigationLifecycleHandling handling = NavigationLifecycleHandling.Handled, object? data = null)
    {
        EventType = eventType;
        Target = target;
        Handling = handling;
        Data = data;
    }
}
