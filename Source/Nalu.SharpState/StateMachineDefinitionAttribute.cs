namespace Nalu.SharpState;

/// <summary>
/// Marks a partial class as a state machine definition operating over a given context type.
/// The source generator scans the class for <see cref="StateTriggerDefinitionAttribute"/>-annotated
/// partial methods (to discover triggers) and <see cref="StateDefinitionAttribute"/>-annotated static
/// properties (to discover states) and emits the matching state/trigger enums, configurator, and
/// <c>Instance</c> runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class StateMachineDefinitionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineDefinitionAttribute"/>.
    /// </summary>
    /// <param name="contextType">The type of the context on which the machine operates.</param>
    public StateMachineDefinitionAttribute(Type contextType)
    {
        ContextType = contextType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineDefinitionAttribute"/> with
    /// <see cref="object"/> as the context type.
    /// </summary>
    public StateMachineDefinitionAttribute()
        : this(typeof(object))
    { }

    /// <summary>
    /// The type of the context on which the machine operates.
    /// </summary>
    public Type ContextType { get; }

    /// <summary>
    /// When <c>true</c>, every generated trigger method on the <c>Instance</c> class returns <see cref="ValueTask"/>
    /// and dispatches through <see cref="StateMachineEngine{TContext, TState, TTrigger}.FireAsync"/>, and the
    /// generated configurator exposes the async-only builder interface (<c>InvokeAsync</c>).
    /// When <c>false</c> (default), triggers return <c>void</c> and the configurator exposes the sync-only builder
    /// interface (<c>Invoke</c>).
    /// </summary>
    public bool Async { get; init; }
}
