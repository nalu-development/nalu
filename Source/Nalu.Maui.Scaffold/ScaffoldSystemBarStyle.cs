namespace Nalu;

/// <summary>
/// The style of the system status bar (and, on Android, gesture navigation bar) icons over a
/// scaffold page — settable per page, per <see cref="ScaffoldArea"/> or per scaffold via
/// <see cref="Scaffold.SystemBarStyleProperty"/> (most specific non-<see cref="Auto"/> wins).
/// </summary>
/// <remarks>
/// The declared value describes the PAGE'S OWN surface: whenever an opaque chrome layer covers
/// the status-bar region (a materialized nav bar, an open flyout), that layer's brightness wins
/// regardless of the declaration — the icons always contrast with what is actually visible.
/// </remarks>
public enum ScaffoldSystemBarStyle
{
    /// <summary>
    /// Derive the style from the visible surface: an overlay covering the status bar (open
    /// flyout), then the nav bar background when the bar is shown and sufficiently opaque,
    /// then the sampled luminance of the rendered status-bar strip, then the page's (or its
    /// top-spanning first child's) background color, then the app theme. The default.
    /// </summary>
    Auto,

    /// <summary>Light (white) icons — for pages whose top-of-screen surface is dark.</summary>
    LightContent,

    /// <summary>Dark (black) icons — for pages whose top-of-screen surface is light.</summary>
    DarkContent
}
