namespace Nalu;

/// <summary>
/// Nav bar drawer-button policy for a flyout, attached via
/// <see cref="Scaffold.FlyoutStartButtonVisibilityProperty"/> /
/// <see cref="Scaffold.FlyoutEndButtonVisibilityProperty"/> alongside the flyout content.
/// The button only ever shows when the corresponding flyout content resolves.
/// </summary>
public enum ScaffoldFlyoutButtonVisibility
{
    /// <summary>The drawer button shows at stack roots and yields while pages are pushed (the default).</summary>
    Auto,

    /// <summary>The drawer button always shows (side by side with the back button while pages are pushed).</summary>
    Visible,

    /// <summary>The drawer button never shows.</summary>
    Hidden
}
