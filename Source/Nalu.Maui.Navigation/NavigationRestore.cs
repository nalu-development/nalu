using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Nalu;

/// <summary>
/// Serializes captured intents into the navigation-state snapshot and back. Intents are plain
/// objects serialized as-is (JSON by default): mark properties that cannot (or should not)
/// serialize with <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/> and
/// rehydrate them on restore via <see cref="IIntentHydrator{TIntent}"/>.
/// The default implementation uses System.Text.Json reflection; supply a source-generated
/// <see cref="JsonSerializerContext"/> via <see cref="NavigationRestoreOptions.IntentSerializerContext"/>
/// (or replace this service in DI) for trimming/NativeAOT-safe apps.
/// </summary>
public interface IIntentSerializer
{
    /// <summary>Serializes the intent to its wire payload.</summary>
    /// <param name="intent">The intent instance; its runtime type is registered via <see cref="NavigationRestoreOptions.AddIntent{T}"/>.</param>
    string Serialize(object intent);

    /// <summary>Deserializes a wire payload back into the given registered intent type.</summary>
    /// <param name="intentType">The registered intent type resolved from the snapshot's stable type id.</param>
    /// <param name="payload">The wire payload produced by <see cref="Serialize"/>.</param>
    object Deserialize(Type intentType, string payload);
}

/// <summary>
/// Rehydrates a restored intent's non-serialized state before its page is recreated.
/// Properties marked with <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>
/// come back default after a restart; during the replay, BEFORE navigating with a restored
/// intent, the engine walks the already-restored stack from the TOP page down to the root
/// looking for the first lifecycle target (page model, or the page itself) implementing this
/// interface for the intent's type, and awaits <see cref="HydrateAsync"/> so it can fill the
/// missing properties — then navigates with the finalized intent.
/// </summary>
/// <typeparam name="TIntent">The intent type to hydrate.</typeparam>
public interface IIntentHydrator<in TIntent>
{
    /// <summary>Fills the intent's non-serialized properties (e.g. reloading them from persistence).</summary>
    /// <param name="intent">The deserialized intent about to be replayed.</param>
    ValueTask HydrateAsync(TIntent intent);
}

/// <summary>
/// Persists the navigation-state snapshot. The default store is a JSON file in the app cache
/// directory; replace in DI for custom locations or formats.
/// </summary>
public interface INavigationRestoreStore
{
    /// <summary>
    /// Reads the persisted snapshot and DELETES it in the same operation (crash-loop
    /// containment: a replay that crashes the app yields a clean next boot). Called once
    /// during startup — must be synchronous and fast. Returns null when no snapshot exists
    /// (or reading fails — restore is fail-open).
    /// </summary>
    string? ReadAndDelete();

    /// <summary>
    /// Persists a snapshot, replacing any previous one. Called debounced in the background;
    /// exceptions are swallowed by the caller (capture must never affect navigation).
    /// </summary>
    Task WriteAsync(string snapshot, CancellationToken cancellationToken);
}

/// <summary>
/// Navigation-state snapshot &amp; restore (opt-in via
/// <c>builder.UseNaluNavigationRestore(...)</c>): after an app restart the engine
/// replays the last captured navigation — root selection, pushed stack and the intents that
/// materialized those pages — landing the user exactly where they were. Restore replays
/// <b>navigation</b>; it never serializes page UI state.
/// </summary>
/// <remarks>
/// <para>
/// Capture is automatic: every successful navigation records the current stack, and each
/// page's <b>entering intent</b> is serialized at navigation time. A page is restorable when
/// it was navigated to without an intent, or with an intent whose type is registered via
/// <see cref="NavigationRestoreOptions.AddIntent{T}"/>; a page reached with an unregistered
/// intent (or one that fails to serialize) ends the restorable stack at that page, and
/// nothing above it restores.
/// Non-serializable intent properties are excluded with
/// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/> and rehydrated on
/// restore via <see cref="IIntentHydrator{TIntent}"/>.
/// </para>
/// <para>
/// Replay happens at engine startup, AFTER the configured initial page's first
/// <c>OnAppearingAsync</c> completes — an app's initialization root always runs first. While
/// a restore is pending, navigations not issued by the replay are ignored (returning
/// <c>false</c> and raising the <c>NavigationIgnored</c> lifecycle event): each replay step
/// is enqueued through the dispatcher, so auto-navigations dispatched by intermediate
/// restored pages deterministically drain inside the suppression window. The window lifts
/// just before the LAST restored destination — the page the user actually was on keeps its
/// right to auto-navigate. An initialization flow that must redirect elsewhere
/// (e.g. authentication) calls <see cref="TryStopRestoreAsync"/> first.
/// </para>
/// <para>
/// The service is always injectable and INERT when restore is not enabled — shared and
/// library pages can call it unconditionally. The per-page methods deduce the page they act
/// on: the page whose lifecycle callback is running, or the current top page otherwise.
/// </para>
/// </remarks>
public interface INavigationRestore
{
    /// <summary>
    /// Removes the current page from the restoration stack: a restore lands on the page
    /// below it (pages above cannot restore either — their context builds on this one).
    /// Typical use: entity-creation flows and wizard pages that must never resurrect, calling
    /// this from <c>OnEnteringAsync</c>. The exclusion lasts until the page pops.
    /// The snapshot is re-captured and persisted before the returned task completes.
    /// </summary>
    Task ForgetAsync();

    /// <summary>
    /// Sets or replaces the intent replayed for the current page on restore — e.g. swapping a
    /// "create draft" intent for a "saved entity id" intent once state materializes, or
    /// re-opting-in a page whose original intent was not restorable. The intent type must be
    /// registered via <see cref="NavigationRestoreOptions.AddIntent{T}"/>; it is serialized
    /// immediately (failures throw at this call site), and the snapshot is re-captured and
    /// persisted before the returned task completes.
    /// </summary>
    /// <param name="intent">The intent to deliver to this page when it is restored.</param>
    Task RestoreWithIntentAsync(object intent);

    /// <summary>
    /// Stops a pending (or in-flight) restore: the persisted snapshot is discarded, the
    /// replay stops after the current navigation, and the navigation-suppression window is
    /// lifted. Returns true when there was a restore to stop. Call this from an
    /// initialization flow that must navigate elsewhere (e.g. to a login page) instead of
    /// letting the replay land the user back in the app.
    /// </summary>
    Task<bool> TryStopRestoreAsync();
}

/// <summary>
/// Configures navigation-state snapshot &amp; restore inside
/// <c>builder.UseNaluNavigationRestore(...)</c>.
/// </summary>
public sealed class NavigationRestoreOptions
{
    private readonly Dictionary<string, Type> _intentTypesById = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _intentIdsByType = [];

    /// <summary>
    /// Whether restore is active. Defaults to true (calling <c>UseNaluNavigationRestore</c> is
    /// the opt-in); the library cannot see the app's build configuration, so a DEBUG-only policy
    /// is expressed app-side: <c>options.Enabled = isDebugBuild</c> (or wrap the whole
    /// <c>UseNaluNavigationRestore</c> call in <c>#if DEBUG</c>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Discards snapshots older than this age at boot. Null (the default) never expires —
    /// appropriate for the DEBUG developer-experience use; production apps usually want one.
    /// </summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>
    /// Optional source-generated <see cref="JsonSerializerContext"/> used by the default
    /// <see cref="IIntentSerializer"/> instead of reflection — required for NativeAOT.
    /// Every type registered via <see cref="AddIntent{T}"/> must be included in the context.
    /// </summary>
    public JsonSerializerContext? IntentSerializerContext { get; set; }

    /// <summary>
    /// Registers an intent type under a stable snapshot type id (never an assembly-qualified
    /// type name: renames only invalidate, they never deserialize the wrong thing). Only
    /// registered intent types are restorable — the registration also preserves the type's
    /// constructors, properties and fields under trimming, so the default JSON serializer
    /// round-trips it reliably. Exclude non-serializable properties with
    /// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/> and rehydrate them on
    /// restore via <see cref="IIntentHydrator{TIntent}"/>.
    /// </summary>
    /// <typeparam name="T">The intent type.</typeparam>
    /// <param name="typeId">The stable id; defaults to the type's short name. Collision-checked.</param>
    public NavigationRestoreOptions AddIntent<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.NonPublicConstructors
            | DynamicallyAccessedMemberTypes.PublicProperties
            | DynamicallyAccessedMemberTypes.PublicFields
        )] T>(string? typeId = null)
    {
        var type = typeof(T);
        typeId ??= type.Name;

        if (_intentTypesById.TryGetValue(typeId, out var existing) && existing != type)
        {
            throw new InvalidOperationException(
                $"Intent type id '{typeId}' is already registered for {existing.FullName}; pass an explicit typeId for {type.FullName}."
            );
        }

        _intentTypesById[typeId] = type;
        _intentIdsByType[type] = typeId;

        return this;
    }

    internal IReadOnlyDictionary<string, Type> IntentTypesById => _intentTypesById;
    internal IReadOnlyDictionary<Type, string> IntentIdsByType => _intentIdsByType;
}
