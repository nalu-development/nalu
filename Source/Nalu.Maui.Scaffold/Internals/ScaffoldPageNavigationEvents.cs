using System.Runtime.CompilerServices;

namespace Nalu.Internals;

/// <summary>
/// Raises MAUI's page navigation events (<c>NavigatedFrom</c>/<c>NavigatedTo</c>) on
/// scaffold-hosted pages. The senders are INTERNAL in MAUI (only its own hosts call them),
/// yet real behavior keys on them — e.g. <c>Page.HideSoftInputOnTapped</c> is gated on
/// <c>Page.HasNavigatedTo</c>, which only <c>SendNavigatedTo</c> sets — so a custom host that
/// never raises them silently disables such features.
/// </summary>
/// <remarks>
/// <see cref="UnsafeAccessorAttribute"/> instead of reflection: typed signatures, no trimming
/// annotations, and a MAUI shape change surfaces as a NativeAOT publish-time error (or a loud
/// <see cref="MissingMethodException"/> on first navigation under JIT) instead of a silent
/// degradation.
/// </remarks>
internal static class ScaffoldPageNavigationEvents
{
    /// <summary>
    /// Sends <c>NavigatedFrom</c> to the outgoing page (when any) and <c>NavigatedTo</c> to
    /// the incoming one, in MAUI's order. <c>SendNavigatedFrom</c> is invoked with
    /// <c>disconnectHandlers: false</c> — the scaffold owns page/handler lifetime (covered
    /// pages are preserved; removed pages run the existing disconnection lifecycle).
    /// </summary>
    public static void SendNavigated(Page? from, Page to, NavigationType navigationType)
    {
        if (from is not null)
        {
            SendNavigatedFrom(from, CreateNavigatedFromEventArgs(to, navigationType), disconnectHandlers: false);
        }

        SendNavigatedTo(to, CreateNavigatedToEventArgs(from, navigationType));
    }

    /// <summary>
    /// Sends MAUI's <c>Disappearing</c> to the outgoing page and <c>Appearing</c> to the incoming
    /// one, in that order — the events every MAUI page assumes fire when it is covered or shown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A custom host must raise these itself. MAUI only propagates them automatically from a
    /// <see cref="IPageContainer{T}"/>'s own appearing to its <c>CurrentPage</c>, which covers the
    /// scaffold's FIRST presentation and nothing after it: every push, pop and root switch is ours
    /// to announce.
    /// </para>
    /// <para>
    /// Both senders are guarded inside MAUI (<c>SendAppearing</c> no-ops when the page already
    /// appeared, <c>SendDisappearing</c> when it never did), so re-synchronizing an unchanged
    /// presentation cannot double-fire.
    /// </para>
    /// </remarks>
    public static void SendAppearanceChange(Page? from, Page to)
    {
        if (from is not null && !ReferenceEquals(from, to))
        {
            ((IPageController) from).SendDisappearing();
        }

        ((IPageController) to).SendAppearing();
    }

    /// <summary>Maps a presentation hint onto MAUI's constrained <see cref="NavigationType"/> (no tab-switch value exists: slides read as pushes).</summary>
    public static NavigationType ToNavigationType(this ScaffoldPresentationHint hint)
        => hint == ScaffoldPresentationHint.Pop ? NavigationType.Pop : NavigationType.Push;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendNavigatedTo")]
    private static extern void SendNavigatedTo(Page page, NavigatedToEventArgs args);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SendNavigatedFrom")]
    private static extern void SendNavigatedFrom(Page page, NavigatedFromEventArgs args, bool disconnectHandlers);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern NavigatedToEventArgs CreateNavigatedToEventArgs(Page? previousPage, NavigationType navigationType);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern NavigatedFromEventArgs CreateNavigatedFromEventArgs(Page destinationPage, NavigationType navigationType);
}
