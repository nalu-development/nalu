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
    /// <see cref="ScaffoldRoot.NavigationStack"/> model, animating per <paramref name="hint"/>,
    /// and updates the chrome (tab bar visibility and its safe-area footprint contribution).
    /// Closes any open overlay first (navigation dismisses drawers and panels).
    /// </summary>
    Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint);

    /// <summary>Presents the given content as a flyout sliding in from the given side (full scrim behind).</summary>
    Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content);

    /// <summary>
    /// Opens a panel anchored above the bottom chrome, with a fullscreen scrim inserted BELOW
    /// the tab bar strip in z-order — the bar stays undimmed and interactive (tapping an in-bar
    /// item both dismisses the panel and performs that selection). Used by the default
    /// template's "More" overflow and by <see cref="Scaffold.OpenTabBarPanelAsync"/>.
    /// </summary>
    /// <param name="content">The panel view (its horizontal margin positions it).</param>
    /// <param name="scrimColor">The scrim color.</param>
    /// <param name="disconnectOnClose">Whether to disconnect the content's handlers on close (single-use panels).</param>
    /// <param name="cleanup">Invoked when the overlay closes (or when presenting fails).</param>
    Task OpenTabBarPanelAsync(View content, Color scrimColor, bool disconnectOnClose, Action? cleanup);

    /// <summary>Gets whether an overlay (flyout or overflow panel) is currently presented.</summary>
    bool HasOverlay { get; }

    /// <summary>Dismisses the current overlay, if any. Back gestures dismiss overlays before the navigation engine is consulted.</summary>
    Task CloseOverlayAsync();
}

/// <summary>How a <see cref="IScaffoldPresenter.SynchronizeAsync"/> pass should be animated.</summary>
internal enum ScaffoldPresentationHint
{
    /// <summary>No animation: initial display, stack trimming, restore replay.</summary>
    None,

    /// <summary>Forward transition to the new top page (single animation even for multi-push batches).</summary>
    Push,

    /// <summary>Backward transition revealing the new top page.</summary>
    Pop,

    /// <summary>
    /// Root/area switch toward a LOWER ordinal (an earlier tab): the new content enters from
    /// the start edge. Logical direction — presenters map it to the physical edge (RTL-aware
    /// mapping arrives with the transition engine).
    /// </summary>
    SlideStart,

    /// <summary>
    /// Root/area switch toward a HIGHER ordinal (a later tab): the new content enters from
    /// the end edge.
    /// </summary>
    SlideEnd
}
