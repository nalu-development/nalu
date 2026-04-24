namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Equatable DTO describing one state declared with <c>[StateDefinition]</c>.
/// </summary>
/// <param name="Name">The property name (also the State enum member).</param>
/// <param name="ParentState">Name of the composite parent state (set by an enclosing <c>[SubStateMachine]</c> region), or null for root-level states.</param>
/// <param name="InitialChildState">Name of the initial-child state (set when this state is the <c>Parent</c> of some <c>[SubStateMachine]</c> region), or null otherwise.</param>
/// <param name="IsInitial">Whether this state is marked as the initial state of its containing region.</param>
/// <param name="RegionPath">Dotted chain of nested <c>[SubStateMachine]</c> class names (from the outer machine body) that contains the state, or empty for root-level states.</param>
/// <param name="DocumentationCommentId">Documentation comment target used for generated <c>inheritdoc</c> comments.</param>
internal readonly record struct StateModel(
    string Name,
    string? ParentState,
    string? InitialChildState,
    bool IsInitial,
    string RegionPath,
    string? DocumentationCommentId);
