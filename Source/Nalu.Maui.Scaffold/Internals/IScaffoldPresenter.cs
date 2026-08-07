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

    /// <summary>
    /// Presents an overlay entry per its request (kind selects z-slot, geometry and animation);
    /// entries STACK in open order within their slot, each above its own scrim. Returns false —
    /// after invoking the request's cleanup — when presentation is impossible (no platform
    /// view). Single-instance policies (one flyout, one tab bar panel) are the SCAFFOLD's
    /// responsibility; the presenter trusts its input.
    /// </summary>
    Task<bool> ShowOverlayAsync(ScaffoldOverlayRequest request);

    /// <summary>
    /// Swaps the presented tab bar panel's content and scrim in place (content crossfade, no
    /// scrim re-animation): the <see cref="Scaffold.ShowTabBarPanelAsync"/> replace path.
    /// The replaced request's cleanup runs; the replacement takes over the entry identity.
    /// </summary>
    Task ReplaceTabBarPanelAsync(ScaffoldOverlayRequest replacement);

    /// <summary>Closes the given entry (no-op when it is not presented). Idempotent and safe under concurrent calls.</summary>
    Task CloseOverlayAsync(ScaffoldOverlayRequest request);

    /// <summary>
    /// Back-gesture policy: closes the TOPMOST entry when it allows back-dismissal, otherwise
    /// consumes the gesture without closing. Overlays dismiss before the navigation engine is
    /// ever consulted.
    /// </summary>
    Task CloseTopOverlayAsync();

    /// <summary>Closes every presented entry (navigation commits dismiss all overlays).</summary>
    Task CloseAllOverlaysAsync();

    /// <summary>Gets whether any overlay entry is presented.</summary>
    bool HasOverlay { get; }

    /// <summary>Gets whether the given entry is currently presented.</summary>
    bool IsOverlayPresented(ScaffoldOverlayRequest request);
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
    SlideEnd,

    /// <summary>
    /// Root switch ACROSS areas (a different tab bar, a flyout destination): the two roots are
    /// not neighbours on a strip, so there is no direction to travel in — the outgoing content
    /// fades out over the new one.
    /// </summary>
    Fade
}
