namespace Nalu;

/// <summary>
/// Tunes how the Nalu navigation source generator registers an intent type for
/// navigation-state restore (the generated <c>AddIntents()</c>). The attribute only governs
/// the automatic restore registration — an intent works as a navigation intent with or
/// without it.
/// </summary>
/// <remarks>
/// Without the attribute an intent discovered through <see cref="IEnteringAware{TIntent}"/> /
/// <see cref="IAppearingAware{TIntent}"/> implementations is registered as restorable under its
/// short type name. Intents implementing <see cref="IAwaitableIntentController"/> (e.g.
/// deriving from <see cref="AwaitableIntent"/>) are never registered: their completion source
/// cannot survive an app restart.
/// </remarks>
/// <param name="typeId">
/// Optional stable snapshot type id used instead of the type's short name — the generated
/// registration passes it as the <c>typeId</c> of
/// <see cref="NavigationRestoreOptions.AddIntent{T}"/>: renames only invalidate old snapshots,
/// they never deserialize the wrong thing. Required (enforced by a generator diagnostic) when
/// two restorable intents share the same short name.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class AutoNavigationIntentAttribute(string? typeId = null) : Attribute
{
    /// <summary>
    /// Gets the stable snapshot type id; null uses the type's short name.
    /// (Named <c>Id</c> because <see cref="Attribute.TypeId"/> already exists.)
    /// </summary>
    public string? Id { get; } = typeId;

    /// <summary>
    /// Whether the automatic restore registration is enabled for this intent. Defaults to true.
    /// When false the intent is not restorable: a page reached with it ends the restorable
    /// stack (it and the pages above it are not restored) — the pre-existing behavior for
    /// unregistered intents.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
