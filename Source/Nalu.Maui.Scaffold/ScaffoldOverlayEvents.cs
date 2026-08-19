namespace Nalu;

/// <summary>What kind of overlay an overlay event is about.</summary>
public enum ScaffoldOverlayKind
{
    /// <summary>An edge drawer (top layer, slides from the side). Single instance.</summary>
    Flyout,

    /// <summary>The tab bar panel (below the tab bar strip in z-order, rises above the bottom chrome). Single instance.</summary>
    TabBarPanel,

    /// <summary>A popup (top layer, placed per <see cref="ScaffoldPopupOptions"/>, fades in). Stacks freely.</summary>
    Popup,

    /// <summary>A bottom sheet (top layer, slides from the bottom edge). Stacks freely.</summary>
    BottomSheet
}

/// <summary>The lifecycle moments reported by <see cref="Scaffold.OverlayEvent"/>.</summary>
public enum ScaffoldOverlayEventType
{
    /// <summary>The overlay is on screen (its presentation succeeded).</summary>
    Presented,

    /// <summary>The overlay left the screen — whatever closed it (handle, scrim tap, system back, pull-down, navigation, replacement).</summary>
    Closed
}

/// <summary>
/// Describes an overlay lifecycle event raised by <see cref="Scaffold.OverlayEvent"/>: one
/// <see cref="ScaffoldOverlayEventType.Presented"/> / <see cref="ScaffoldOverlayEventType.Closed"/>
/// pair per overlay instance, after the fact (never cancelable). Overlays are not pages, so
/// they never appear in <see cref="Scaffold.NavigationEvent"/>; this is the hook for analytics
/// (screen-like tracking of popups, sheets, drawers and the tab bar panel) and diagnostics.
/// </summary>
public sealed class ScaffoldOverlayEventArgs : EventArgs
{
    /// <summary>Gets the overlay kind.</summary>
    public ScaffoldOverlayKind Kind { get; }

    /// <summary>Gets the lifecycle moment.</summary>
    public ScaffoldOverlayEventType EventType { get; }

    /// <summary>Gets the presented view (for a bottom sheet: the content you passed, not the sheet wrapper).</summary>
    public View Content { get; }

    /// <summary>
    /// Gets the overlay model for overlays shown through <see cref="IOverlayService"/> (the
    /// view itself for view-only registrations); <c>null</c> for view-level presentations
    /// (<see cref="Scaffold.ShowPopupAsync"/>, <see cref="Scaffold.ShowBottomSheetAsync"/>,
    /// <see cref="Scaffold.ShowTabBarPanelAsync"/>, flyouts).
    /// </summary>
    public object? Model { get; }

    /// <summary>Gets the intent delivered to the overlay model (<see cref="IOverlayService"/> overlays only).</summary>
    public object? Intent { get; }

    /// <summary>
    /// Gets the result the overlay closed with (<see cref="ScaffoldOverlayEventType.Closed"/> of an
    /// <see cref="IOverlayService"/> overlay whose model called <see cref="IOverlayRef.CloseAsync(object?)"/>);
    /// <c>null</c> otherwise.
    /// </summary>
    public object? Result { get; }

    /// <summary>Gets the drawer side for <see cref="ScaffoldOverlayKind.Flyout"/> events; <c>null</c> otherwise.</summary>
    public ScaffoldFlyoutSide? FlyoutSide { get; }

    internal ScaffoldOverlayEventArgs(
        ScaffoldOverlayKind kind,
        ScaffoldOverlayEventType eventType,
        View content,
        object? model,
        object? intent,
        object? result,
        ScaffoldFlyoutSide? flyoutSide)
    {
        Kind = kind;
        EventType = eventType;
        Content = content;
        Model = model;
        Intent = intent;
        Result = result;
        FlyoutSide = flyoutSide;
    }
}
