namespace Nalu;

/// <summary>
/// Bridges component-based UI frameworks (MauiReactor, Comet, …) into Nalu navigation:
/// turns a component instance into the native <see cref="Page" /> it renders.
/// Register an implementation via <c>UseComponentPageFactory&lt;TFactory&gt;()</c> to enable
/// <see cref="NavigationConfigurator.AddPage{TPage}" /> with non-<see cref="Page" /> types —
/// the MauiReactor guide in the conceptual docs carries a ready-to-paste implementation.
/// </summary>
/// <remarks>
/// The factory runs inside the page's own navigation DI scope: the component instance is
/// resolved from that scope (constructor injection works) and handed over as-is. The returned
/// handle owns the mounted component tree — Nalu disposes it when the page leaves the
/// navigation stack, BEFORE the page's service scope is disposed.
/// </remarks>
public interface IComponentPageFactory
{
    /// <summary>
    /// Mounts <paramref name="component" /> and returns a handle to the native page it renders.
    /// Must complete synchronously: the page is pushed (and its presentation mode read)
    /// immediately after this call returns.
    /// </summary>
    /// <param name="component">The component instance, resolved from the page's navigation scope.</param>
    /// <exception cref="InvalidOperationException">The component does not render a <see cref="Page" />.</exception>
    IComponentPageHandle CreatePage(object component);
}

/// <summary>
/// A mounted component-based page created by an <see cref="IComponentPageFactory" />.
/// </summary>
public interface IComponentPageHandle : IDisposable
{
    /// <summary>
    /// Gets the native page rendered by the component. Stable for the lifetime of the handle:
    /// re-renders update this page in place.
    /// </summary>
    Page Page { get; }

    /// <summary>
    /// Gets the object receiving Nalu navigation lifecycle callbacks
    /// (<see cref="IEnteringAware" />, <see cref="ILeavingGuard" />, intent methods…) —
    /// normally the component itself. Read on EVERY dispatch, never cached: an adapter may
    /// redirect it (e.g. to a hot-reloaded replacement component).
    /// </summary>
    object LifecycleTarget { get; }
}
