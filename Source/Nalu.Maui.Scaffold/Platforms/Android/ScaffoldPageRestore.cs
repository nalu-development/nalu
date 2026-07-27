using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// Undoes the render state the androidx shared-element machinery leaves on the OUTGOING source
/// views of a push (§8 Android gotchas): the SET hides them via <c>setTransitionAlpha(0)</c> —
/// invisible to <c>getAlpha()</c>, drawable/visibility/matrix/clip all read healthy — and only
/// the paired RETURN transition restores them. A predictive-back pop skips the SET (the handoff
/// adopts the settled visuals), so without repair the views stay undrawn forever.
/// The sources are captured at the moment the presenter wires them into the transaction, so the
/// repair touches exactly those views — no tree walk.
/// </summary>
internal static class ScaffoldPageRestore
{
    private static readonly ConditionalWeakTable<Page, IReadOnlyList<View>> _sharedElementSources = [];

    /// <summary>
    /// Records the views a shared-element transition is about to take off from, keyed by their
    /// (outgoing) page. Entry lifetime is tied to the page; a later capture replaces it.
    /// </summary>
    public static void CaptureSharedElementSources(Page page, IReadOnlyList<View> views)
        => _sharedElementSources.AddOrUpdate(page, views);

    /// <summary>
    /// Restores drawability of the captured source views when their page is shown again on a
    /// path that skips the return SET (predictive-back peek and fragment remount). One-shot per
    /// capture; a no-op for pages without one. A normal pop repairs harmlessly ahead of its SET,
    /// which then manages the same views itself.
    /// </summary>
    public static void Repair(Page page)
    {
        if (!_sharedElementSources.TryGetValue(page, out var views))
        {
            return;
        }

        _sharedElementSources.Remove(page);

        foreach (var view in views)
        {
            if (view.Handler is not IPlatformViewHandler { PlatformView: { } platformView } handler)
            {
                continue;
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                platformView.TransitionAlpha = 1f;
            }

            // Undo anything else the transition framework left on the platform view
            // (visibility, alpha, translation, transform) by re-running the mappers.
            handler.UpdateValue(nameof(IView.Visibility));
            handler.UpdateValue(nameof(IView.Opacity));
            handler.UpdateValue(nameof(IView.TranslationX));
            handler.UpdateValue(nameof(IView.TranslationY));
            handler.UpdateValue(nameof(IView.Scale));
            handler.UpdateValue(nameof(IView.Rotation));
        }
    }
}
