namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Equatable DTO describing one trigger declared with <c>[StateTriggerDefinition]</c>.
/// </summary>
/// <param name="Name">The trigger method name (also the Trigger enum member).</param>
/// <param name="Parameters">The parameter list of the trigger method.</param>
/// <param name="DocumentationCommentId">Documentation comment target used for generated <c>inheritdoc</c> comments.</param>
internal readonly record struct TriggerModel(
    string Name,
    EquatableArray<ParameterModel> Parameters,
    string? DocumentationCommentId);
