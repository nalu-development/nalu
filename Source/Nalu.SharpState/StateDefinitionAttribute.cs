namespace Nalu.SharpState;

/// <summary>
/// Marks a static property on a <see cref="StateMachineDefinitionAttribute"/>-annotated class as a state
/// definition. The property name becomes a member of the generated <c>State</c> enum, and the property body
/// (built via <c>ConfigureState()</c>) provides the transitions and hierarchy metadata for that state.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class StateDefinitionAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether this state is the initial state of its containing region.
    /// Exactly one state per region must set this to <c>true</c>.
    /// </summary>
    public bool Initial { get; set; }
}
