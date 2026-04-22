namespace Nalu.SharpState;

/// <summary>
/// Marks a <c>partial void</c> method as a state machine trigger definition.
/// The method name becomes a member of the generated <c>Trigger</c> enum and its parameter list becomes
/// the trigger's dispatch signature exposed on the generated <c>Instance</c> class.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StateTriggerDefinitionAttribute : Attribute
{
}
