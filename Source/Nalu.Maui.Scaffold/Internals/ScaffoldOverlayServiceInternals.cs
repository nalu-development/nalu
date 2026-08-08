using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Nalu;

/// <summary>One registered model-first overlay pairing (trim-safe: generics captured in closures).</summary>
internal sealed record ScaffoldOverlayRegistration(
    Func<IServiceProvider, object> CreateModel,
    Func<IServiceProvider, object, View> CreateView
);

/// <summary>The overlay registry built by <c>UseNaluScaffold</c>'s configurator.</summary>
internal sealed class ScaffoldOverlayRegistry : IScaffoldConfigurator
{
    private readonly Dictionary<Type, ScaffoldOverlayRegistration> _registrations = [];

    public ScaffoldOverlayRegistration Get(Type modelType)
        => _registrations.TryGetValue(modelType, out var registration)
            ? registration
            : throw new InvalidOperationException(
                $"No overlay is registered for model {modelType.Name}. Register it via UseNaluScaffold(scaffold => scaffold.AddOverlay<{modelType.Name}, TheView>())."
            );

    public IScaffoldConfigurator AddOverlay<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TModel,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TView>()
        where TModel : class
        where TView : View
    {
        _registrations[typeof(TModel)] = new ScaffoldOverlayRegistration(
            static provider => ActivatorUtilities.CreateInstance<TModel>(provider),
            static (provider, model) => ActivatorUtilities.CreateInstance<TView>(new ScaffoldOverlayServiceProvider(provider, typeof(TModel), model))
        );

        return this;
    }

    public IScaffoldConfigurator AddOverlay<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TModel,
        TView>(Func<IServiceProvider, TModel, TView> viewFactory)
        where TModel : class
        where TView : View
    {
        _registrations[typeof(TModel)] = new ScaffoldOverlayRegistration(
            static provider => ActivatorUtilities.CreateInstance<TModel>(provider),
            (provider, model) => viewFactory(provider, (TModel) model)
        );

        return this;
    }

    public IScaffoldConfigurator AddOverlay<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] TView>()
        where TView : View
    {
        // View-only: the view IS the lifecycle target — one instance serves both roles.
        _registrations[typeof(TView)] = new ScaffoldOverlayRegistration(
            static provider => ActivatorUtilities.CreateInstance<TView>(provider),
            static (_, model) => (View) model
        );

        return this;
    }
}

/// <summary>
/// Serves the presentation-local instances (<see cref="IOverlayRef"/>, the model) FIRST,
/// delegating everything else to the scope provider — so overlay model and view constructors
/// declare exactly the subset they need, with no all-or-nothing argument matching.
/// </summary>
internal sealed class ScaffoldOverlayServiceProvider(IServiceProvider inner, Type localType, object localInstance) : IServiceProvider
{
    public object? GetService(Type serviceType)
        => serviceType == localType ? localInstance : inner.GetService(serviceType);
}

/// <summary>
/// <see cref="IOverlayRef"/> implementation: buffers a close requested BEFORE presentation
/// (e.g. from <c>OnEnteringAsync</c> — the overlay is then never shown), validates the reported
/// result type, and routes the close through the overlay-entry path once presented.
/// </summary>
internal abstract class ScaffoldOverlayRefBase : IOverlayRef
{
    private IScaffoldPopup? _handle;

    /// <summary>Whether a close was requested before the presentation started.</summary>
    public bool CloseRequestedBeforePresentation { get; private set; }

    /// <summary>Binds the presented handle; replays a buffered early close.</summary>
    public void Bind(IScaffoldPopup handle)
    {
        _handle = handle;

        if (CloseRequestedBeforePresentation)
        {
            _ = handle.CloseAsync();
        }
    }

    public Task CloseAsync()
    {
        if (_handle is { } handle)
        {
            return handle.CloseAsync();
        }

        CloseRequestedBeforePresentation = true;

        return Task.CompletedTask;
    }

    public Task CloseAsync(object? result)
    {
        SetResult(result);

        return CloseAsync();
    }

    /// <summary>Stores (and validates) the reported result.</summary>
    protected abstract void SetResult(object? result);
}

/// <summary>The result-carrying ref of <c>Show*Async&lt;TModel, TResult&gt;</c>.</summary>
internal sealed class ScaffoldOverlayRef<TResult> : ScaffoldOverlayRefBase
{
    /// <summary>The reported result; default until (unless) the model closes with one.</summary>
    public TResult? Result { get; private set; }

    protected override void SetResult(object? result)
        => Result = result switch
        {
            null => default,
            TResult typed => typed,
            _ => throw new InvalidOperationException(
                $"The overlay was shown expecting a {typeof(TResult).Name} result, but the model closed reporting a {result.GetType().Name}."
            )
        };
}

/// <summary>The resultless ref of <c>Show*Async&lt;TModel&gt;</c>.</summary>
internal sealed class ScaffoldVoidOverlayRef : ScaffoldOverlayRefBase
{
    protected override void SetResult(object? result)
        => throw new InvalidOperationException(
            "The overlay was shown without a result type; use the Show*Async<TModel, TResult> overload to await a result."
        );
}

/// <summary>
/// Overlay model lifecycle dispatch, mirroring the navigation engine's conventions: an intent is
/// delivered to a single-parameter <c>OnEnteringAsync</c> whose parameter type the intent is
/// assignable to (found by reflection — the model type's methods are preserved by the
/// <c>AddOverlay</c> annotations); otherwise the parameterless <see cref="IEnteringAware"/> runs.
/// <see cref="ILeavingAware"/> runs when the overlay closes.
/// </summary>
internal static class ScaffoldOverlayLifecycle
{
    public static ValueTask SendEnteringAsync(object model, object? intent)
    {
        if (intent is not null)
        {
#pragma warning disable IL2075 // Overlay model types are annotated with PublicMethods|NonPublicMethods at AddOverlay registration.
            var method = model
                         .GetType()
                         .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .Where(candidate => candidate.Name.EndsWith("OnEnteringAsync", StringComparison.Ordinal) && candidate.ReturnType == typeof(ValueTask))
                         .FirstOrDefault(candidate =>
                             {
                                 var parameters = candidate.GetParameters();

                                 return parameters.Length == 1 && intent.GetType().IsAssignableTo(parameters[0].ParameterType);
                             }
                         );
#pragma warning restore IL2075

            if (method is not null)
            {
                return (ValueTask) method.Invoke(model, [intent])!;
            }
        }

        return model is IEnteringAware enteringAware ? enteringAware.OnEnteringAsync() : ValueTask.CompletedTask;
    }

    public static ValueTask SendLeavingAsync(object model)
        => model is ILeavingAware leavingAware ? leavingAware.OnLeavingAsync() : ValueTask.CompletedTask;

    public static async ValueTask DisposeAsync(object model)
    {
        switch (model)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(true);

                break;

            case IDisposable disposable:
                disposable.Dispose();

                break;
        }
    }
}

/// <summary>
/// <see cref="IOverlayService"/> implementation: resolves the ambient <see cref="Scaffold"/>
/// through the navigation service's shell proxy at call time (no-op result when the app is not
/// scaffold-hosted), builds the model/view chain in a per-presentation DI scope, and disposes
/// everything when the overlay closes.
/// </summary>
internal sealed class ScaffoldOverlayService(INavigationService navigationService, ScaffoldOverlayRegistry registry, IServiceScopeFactory scopeFactory) : IOverlayService
{
    public async Task<TResult?> ShowPopupAsync<TModel, TResult>(object? intent = null, ScaffoldPopupOptions? options = null)
        where TModel : class
    {
        var overlayRef = new ScaffoldOverlayRef<TResult>();
        await ShowCoreAsync<TModel>(overlayRef, intent, sheet: false, options, sheetOptions: null).ConfigureAwait(true);

        return overlayRef.Result;
    }

    public Task ShowPopupAsync<TModel>(object? intent = null, ScaffoldPopupOptions? options = null)
        where TModel : class
        => ShowCoreAsync<TModel>(new ScaffoldVoidOverlayRef(), intent, sheet: false, options, sheetOptions: null);

    public async Task<TResult?> ShowBottomSheetAsync<TModel, TResult>(object? intent = null, ScaffoldBottomSheetOptions? options = null)
        where TModel : class
    {
        var overlayRef = new ScaffoldOverlayRef<TResult>();
        await ShowCoreAsync<TModel>(overlayRef, intent, sheet: true, popupOptions: null, options).ConfigureAwait(true);

        return overlayRef.Result;
    }

    public Task ShowBottomSheetAsync<TModel>(object? intent = null, ScaffoldBottomSheetOptions? options = null)
        where TModel : class
        => ShowCoreAsync<TModel>(new ScaffoldVoidOverlayRef(), intent, sheet: true, popupOptions: null, options);

    private async Task ShowCoreAsync<TModel>(
        ScaffoldOverlayRefBase overlayRef,
        object? intent,
        bool sheet,
        ScaffoldPopupOptions? popupOptions,
        ScaffoldBottomSheetOptions? sheetOptions)
        where TModel : class
    {
        if (ResolveScaffold() is not { } scaffold)
        {
            return;
        }

        var registration = registry.Get(typeof(TModel));
        var scope = scopeFactory.CreateScope();

        try
        {
            var provider = new ScaffoldOverlayServiceProvider(scope.ServiceProvider, typeof(IOverlayRef), overlayRef);
            var model = registration.CreateModel(provider);
            var view = registration.CreateView(provider, model);

            // Views taking the model usually assign it themselves; make the convention hold
            // regardless so bindings work with either constructor shape. View-only overlays
            // (view == model) keep their BindingContext untouched — self-assignment would
            // sever inherited contexts for no gain.
            if (!ReferenceEquals(view, model))
            {
                view.BindingContext ??= model;
            }

            try
            {
                await ScaffoldOverlayLifecycle.SendEnteringAsync(model, intent).ConfigureAwait(true);

                // A close requested during OnEnteringAsync skips the presentation entirely.
                if (!overlayRef.CloseRequestedBeforePresentation)
                {
                    var handle = sheet
                        ? await scaffold.ShowBottomSheetAsync(view, sheetOptions).ConfigureAwait(true)
                        : await scaffold.ShowPopupAsync(view, popupOptions).ConfigureAwait(true);

                    overlayRef.Bind(handle);

                    // Same-context completion (RunContinuationsAsynchronously TCS owned by the handle).
#pragma warning disable VSTHRD003
                    await handle.Closed.ConfigureAwait(true);
#pragma warning restore VSTHRD003
                }

                await ScaffoldOverlayLifecycle.SendLeavingAsync(model).ConfigureAwait(true);
            }
            finally
            {
                await ScaffoldOverlayLifecycle.DisposeAsync(model).ConfigureAwait(true);
            }
        }
        finally
        {
            scope.Dispose();
        }
    }

    private Scaffold? ResolveScaffold()
        => navigationService is NavigationService { ShellProxyOrDefault: ScaffoldProxy proxy } ? proxy.Scaffold : null;
}
