using Microsoft.CodeAnalysis;

namespace Nalu.SharpState.Generators.Diagnostics;

internal static class Descriptors
{
    private const string _category = "Nalu.SharpState";

    public static readonly DiagnosticDescriptor MachineMustBePartial = new(
        id: "NSS001",
        title: "State machine class must be partial",
        messageFormat: "Class '{0}' is marked with [StateMachineDefinition] but is not declared as partial",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateName = new(
        id: "NSS002",
        title: "Duplicate trigger or state name",
        messageFormat: "Duplicate {0} name '{1}' in state machine '{2}'",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StateReturnType = new(
        id: "NSS003",
        title: "Invalid state property return type",
        messageFormat: "[StateDefinition] property '{0}' must return the machine's IStateConfiguration type",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TriggerMustBePartialVoid = new(
        id: "NSS004",
        title: "Trigger method must be partial void",
        messageFormat: "[StateTriggerDefinition] method '{0}' must be declared as 'partial void'",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SubStateMachineMustBeNestedPartial = new(
        id: "NSS005",
        title: "[SubStateMachine] class must be a nested partial class",
        messageFormat: "[SubStateMachine] class '{0}' must be declared as a partial class nested inside a [StateMachineDefinition] class (directly or inside another [SubStateMachine] class)",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial = new(
        id: "NSS006",
        title: "Containing type must be partial",
        messageFormat: "State machine class '{0}' is nested inside non-partial type '{1}'",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SubStateMachineParentScope = new(
        id: "NSS007",
        title: "[SubStateMachine] parent must come from the enclosing region",
        messageFormat: "[SubStateMachine] on '{0}' declares Parent '{1}' which is not a state defined in the immediately enclosing region",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SubStateMachineInitialScope = new(
        id: "NSS008",
        title: "Each region must declare an initial state",
        messageFormat: "Region '{0}' must declare exactly one [StateDefinition(Initial = true)] state",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TriggerInSubStateMachine = new(
        id: "NSS009",
        title: "Triggers cannot be declared inside a [SubStateMachine]",
        messageFormat: "[StateTriggerDefinition] '{0}' is declared inside [SubStateMachine] '{1}'; triggers must live on the root [StateMachineDefinition] class",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleInitialStates = new(
        id: "NSS010",
        title: "Only one initial state is allowed per region",
        messageFormat: "Region '{0}' declares multiple [StateDefinition(Initial = true)] states; only one is allowed",
        category: _category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

}
