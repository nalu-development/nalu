namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Equatable DTO describing a single trigger parameter.
/// </summary>
/// <param name="Name">The parameter's declared name.</param>
/// <param name="TypeDisplay">Fully-qualified C# type (with <c>global::</c> prefix) of the parameter.</param>
internal readonly record struct ParameterModel(string Name, string TypeDisplay);
