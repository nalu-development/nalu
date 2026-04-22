namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Equatable DTO describing a containing type of the state machine class (for nested-type emission).
/// </summary>
/// <param name="Name">The simple type name of the containing type.</param>
/// <param name="Keyword">C# keyword used to declare the containing type (<c>class</c>, <c>struct</c>, <c>record</c>).</param>
/// <param name="Accessibility">C# accessibility keyword (<c>public</c>, <c>internal</c>, <c>private</c>, <c>protected</c>, etc.).</param>
/// <param name="TypeParameters">Optional type parameter list including angle brackets (e.g. <c>&lt;T&gt;</c>) or empty string.</param>
internal readonly record struct ContainingTypeModel(
    string Name,
    string Keyword,
    string Accessibility,
    string TypeParameters);
