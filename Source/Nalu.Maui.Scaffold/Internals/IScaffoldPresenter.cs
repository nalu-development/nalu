namespace Nalu;

/// <summary>
/// Platform presentation seam of the Scaffold: realizes the navigation-stack model natively —
/// page materialization (<c>Page.ToPlatform</c>/<c>ToHandler</c>: child view controllers on iOS,
/// fragment-hosted platform views on Android), transitions, and MAUI page lifecycle events
/// (<c>SendAppearing</c>/<c>SendDisappearing</c>, handler disconnection).
/// Implementations live with the Scaffold's platform handler, which provides the
/// <c>IMauiContext</c> required to create page handlers.
/// </summary>
/// <remarks>
/// <para>
/// Synchronize-to-model by design: the Scaffold's navigation proxies mutate the
/// <see cref="ScaffoldNavigationStack"/> freely during a navigation batch (Nalu allows multiple
/// pushes/pops per navigation), then <see cref="SynchronizeAsync"/> is awaited ONCE at commit.
/// The presenter diffs its mounted state against the model — mounting missing pages, unmounting
/// removed ones (running their disconnection lifecycle) — and plays a single transition according
/// to the hint. Root/area selection changes are the same operation with a different target root.
/// Deterministic completion: the returned task ends when presentation settled (no Shell-style
/// animation-settling delays).
/// </para>
/// <para>P0 shape — expected to evolve with the transition engine (P2).</para>
/// </remarks>
internal interface IScaffoldPresenter
{
    /// <summary>
    /// Brings the platform view hierarchy in sync with the given root's
    /// <see cref="ScaffoldRoot.NavigationStack"/> model, animating per <paramref name="hint"/>.
    /// Closes any open flyout first (navigation dismisses drawers).
    /// </summary>
    Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint);

    /// <summary>Presents the given content as a flyout sliding in from the given side (scrim behind).</summary>
    Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content);

    /// <summary>Dismisses the open flyout, if any.</summary>
    Task CloseFlyoutAsync();
}

/// <summary>How a <see cref="IScaffoldPresenter.SynchronizeAsync"/> pass should be animated.</summary>
internal enum ScaffoldPresentationHint
{
    /// <summary>No animation: initial display, stack trimming, restore replay.</summary>
    None,

    /// <summary>Forward transition to the new top page (single animation even for multi-push batches).</summary>
    Push,

    /// <summary>Backward transition revealing the new top page.</summary>
    Pop
}
