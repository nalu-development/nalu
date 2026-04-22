namespace Nalu.SharpState;

/// <summary>
/// Marks a nested partial class as a sub-state-machine region. Every <c>[StateDefinition]</c> property
/// declared inside the class is treated as a child of the composite state passed as <c>parent</c>, and
/// the composite's initial-child is set to the state passed as <c>initial</c>.
/// </summary>
/// <remarks>
/// The class carrying this attribute must be a <c>partial</c> nested class within either the outer
/// <c>[StateMachineDefinition]</c> class or another <c>[SubStateMachine]</c> class (strict nesting).
/// Regions may nest arbitrarily. Triggers cannot be declared inside a region.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SubStateMachineAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="SubStateMachineAttribute"/>.
    /// </summary>
    /// <param name="parent">The composite state this region refines. Must be declared in the immediate enclosing region.</param>
    /// <param name="initial">The state entered by default when the composite is targeted. Must be declared inside this region.</param>
    public SubStateMachineAttribute(object parent, object initial)
    {
        Parent = parent;
        Initial = initial;
    }

    /// <summary>The composite state this region refines.</summary>
    public object Parent { get; }

    /// <summary>The state entered by default when the composite is targeted.</summary>
    public object Initial { get; }
}
