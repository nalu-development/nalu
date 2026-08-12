namespace Nalu;

/// <summary>
/// Provides shell navigation abstraction.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the specified model-name-driven path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Must be invoked on the UI thread.</b> Navigation creates pages, sets binding contexts and drives the
    /// shell directly, without marshalling: calling it from a background thread is undefined behavior and will
    /// usually crash on iOS/Android. From a background thread, wrap the call in
    /// <c>MainThread.InvokeOnMainThreadAsync(...)</c> or <c>IDispatcher.DispatchAsync(...)</c>.
    /// </para>
    /// <para>
    /// Concurrent calls are safe: navigations are serialized, only one runs at a time. A call that had to wait is
    /// then re-validated against the shell location it was computed on — if another navigation moved the shell in
    /// the meantime, the queued one is <b>dropped</b>: it returns <see langword="false" /> and raises the
    /// <see cref="NavigationLifecycleEventType.NavigationIgnored" /> event instead of applying to a state it was
    /// not intended for. Always honor the returned value rather than assuming the navigation happened.
    /// </para>
    /// <para>
    /// Re-entrancy is rejected: calling this method while a navigation is in progress on the same asynchronous flow
    /// (typically from <c>OnEnteringAsync</c>, <c>OnAppearingAsync</c> or a guard) throws
    /// <see cref="InvalidNavigationException" />. Dispatch the call with <c>IDispatcher.DispatchAsync(...)</c>
    /// instead. Doing so from <c>OnAppearingAsync</c> is always safe: the engine commits the navigation — and with
    /// it the location this method validates against — <b>before</b> sending the appearing event, so the dispatched
    /// navigation starts from the final location and cannot be superseded by the one that triggered it.
    /// </para>
    /// </remarks>
    /// <param name="navigation">The navigation to apply.</param>
    /// <returns>True if navigation succeeds, false if it was interrupted by guards or ignored due to a concurrent navigation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="navigation" /> is null.</exception>
    /// <exception cref="InvalidOperationException">Throw if target path is not reachable or target page model can't receive specified intent.</exception>
    /// <exception cref="InvalidNavigationException">Thrown if triggered from within an ongoing navigation.</exception>
    Task<bool> GoToAsync(INavigationInfo navigation);
}
