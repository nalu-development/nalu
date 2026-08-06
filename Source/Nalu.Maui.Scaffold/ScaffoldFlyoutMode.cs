namespace Nalu;

/// <summary>
/// Behavior of a scaffold drawer side (<see cref="Scaffold.FlyoutStartModeProperty"/> /
/// <see cref="Scaffold.FlyoutEndModeProperty"/>). A drawer exists only when its content
/// resolves non-null AND its mode resolves to something other than <see cref="Disabled"/> —
/// both sides default to <see cref="Disabled"/>, so enabling a drawer is always explicit.
/// </summary>
public enum ScaffoldFlyoutMode
{
    /// <summary>
    /// The drawer is available at stack roots only: it behaves as <see cref="Flyout"/> while
    /// the current navigation stack has no pushed pages and as <see cref="Disabled"/>
    /// otherwise (mirroring <see cref="ScaffoldFlyoutButtonVisibility.Auto"/>).
    /// </summary>
    Auto,

    /// <summary>No drawer on this side, even when content is configured (the default).</summary>
    Disabled,

    /// <summary>An overlay drawer sliding in from the edge, available on every page.</summary>
    Flyout

    // Future: Sticky — always visible, splitting the screen with the page (tablets).
}
