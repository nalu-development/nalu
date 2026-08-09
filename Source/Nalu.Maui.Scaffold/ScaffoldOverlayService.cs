using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// The presentation handle an overlay MODEL receives (via constructor injection) to close its
/// own overlay — mirroring how page models receive <c>INavigationService</c>.
/// </summary>
public interface IOverlayRef
{
    /// <summary>Closes the overlay without a result (the caller's task completes with <c>default</c>).</summary>
    Task CloseAsync();

    /// <summary>
    /// Closes the overlay reporting a result to the caller.
    /// </summary>
    /// <param name="result">
    /// The result; must be assignable to the <c>TResult</c> the overlay was shown with.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The result is not assignable to the declared <c>TResult</c>, or the overlay was shown
    /// without a result type.
    /// </exception>
    Task CloseAsync(object? result);
}

/// <summary>
/// MVVM overlay presentation: shows model-first popups and bottom sheets registered via
/// <see cref="IScaffoldConfigurator.AddOverlay{TModel,TView}()"/>, mirroring the navigation
/// engine's conventions — intents delivered to <c>OnEnteringAsync(TIntent)</c>,
/// <c>ILeavingAware</c>/<c>IDisposable</c> honored on close, one DI scope per presentation.
/// </summary>
/// <remarks>
/// <para>
/// Construction: the model is created via <c>ActivatorUtilities</c> against a provider serving
/// <see cref="IOverlayRef"/>; the view likewise, with the model additionally resolvable — each
/// constructor declares only what it needs (services, the ref, the model). Keep ONE public
/// constructor per model/view: multi-constructor selection is not service-aware.
/// </para>
/// <para>
/// The returned task completes when the overlay CLOSES, whatever the path: with the result the
/// model reported via <see cref="IOverlayRef.CloseAsync(object?)"/>, or <c>default</c> on
/// dismissal (scrim tap, pull-down, system back, navigation).
/// </para>
/// <para>
/// While the app is not scaffold-hosted (e.g. on platforms without scaffold hosting, such as
/// Windows or Mac Catalyst), every call is a graceful no-op completing immediately with
/// <c>default</c> — shared page models can inject and call the service unconditionally.
/// </para>
/// </remarks>
public interface IOverlayService
{
    /// <summary>Shows a registered popup model and awaits its result.</summary>
    /// <typeparam name="TModel">The registered overlay model.</typeparam>
    /// <typeparam name="TResult">The result type the model reports via <see cref="IOverlayRef.CloseAsync(object?)"/>.</typeparam>
    /// <param name="intent">Optional intent delivered to the model's <c>OnEnteringAsync(TIntent)</c>.</param>
    /// <param name="options">Call-site presentation overrides (each set property wins over the view's attached values).</param>
    Task<TResult?> ShowPopupAsync<TModel, TResult>(object? intent = null, ScaffoldPopupOptions? options = null)
        where TModel : class;

    /// <summary>Shows a registered popup model with no result; the task completes when it closes.</summary>
    /// <typeparam name="TModel">The registered overlay model.</typeparam>
    /// <param name="intent">Optional intent delivered to the model's <c>OnEnteringAsync(TIntent)</c>.</param>
    /// <param name="options">Call-site presentation overrides (each set property wins over the view's attached values).</param>
    Task ShowPopupAsync<TModel>(object? intent = null, ScaffoldPopupOptions? options = null)
        where TModel : class;

    /// <summary>Shows a registered bottom sheet model and awaits its result.</summary>
    /// <typeparam name="TModel">The registered overlay model.</typeparam>
    /// <typeparam name="TResult">The result type the model reports via <see cref="IOverlayRef.CloseAsync(object?)"/>.</typeparam>
    /// <param name="intent">Optional intent delivered to the model's <c>OnEnteringAsync(TIntent)</c>.</param>
    /// <param name="options">Call-site presentation overrides (each set property wins over the view's attached values).</param>
    Task<TResult?> ShowBottomSheetAsync<TModel, TResult>(object? intent = null, ScaffoldBottomSheetOptions? options = null)
        where TModel : class;

    /// <summary>Shows a registered bottom sheet model with no result; the task completes when it closes.</summary>
    /// <typeparam name="TModel">The registered overlay model.</typeparam>
    /// <param name="intent">Optional intent delivered to the model's <c>OnEnteringAsync(TIntent)</c>.</param>
    /// <param name="options">Call-site presentation overrides (each set property wins over the view's attached values).</param>
    Task ShowBottomSheetAsync<TModel>(object? intent = null, ScaffoldBottomSheetOptions? options = null)
        where TModel : class;
}

/// <summary>Configures the scaffold services inside <c>UseNaluScaffold</c>.</summary>
public interface IScaffoldConfigurator
{
    /// <summary>
    /// Registers a model-first overlay pairing for <see cref="IOverlayService"/>: the view is
    /// instantiated per presentation with the model (and any registered services) resolvable
    /// through its constructor.
    /// </summary>
    /// <typeparam name="TModel">The overlay model; gets <see cref="IOverlayRef"/> and services via its single public constructor.</typeparam>
    /// <typeparam name="TView">The overlay view; gets the model and services via its single public constructor.</typeparam>
    IScaffoldConfigurator AddOverlay<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TModel,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>()
        where TModel : class
        where TView : View;

    /// <summary>
    /// Registers a model-first overlay pairing with an explicit view factory — the zero-magic
    /// escape hatch (no reflection over <typeparamref name="TView"/>'s constructors).
    /// </summary>
    IScaffoldConfigurator AddOverlay<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TModel,
        TView>(Func<IServiceProvider, TModel, TView> viewFactory)
        where TModel : class
        where TView : View;

    /// <summary>
    /// Registers a VIEW-ONLY overlay: the view is its own lifecycle target — it receives
    /// <see cref="IOverlayRef"/> (and services) via its single public constructor, intents are
    /// delivered to its <c>OnEnteringAsync</c>, and it is shown via
    /// <c>Show*Async&lt;TView&gt;()</c>. Its <c>BindingContext</c> is left untouched.
    /// </summary>
    /// <typeparam name="TView">The overlay view, acting as its own model.</typeparam>
    IScaffoldConfigurator AddOverlay<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TView>()
        where TView : View;
}
