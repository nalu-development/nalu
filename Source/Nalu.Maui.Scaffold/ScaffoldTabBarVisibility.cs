namespace Nalu;

/// <summary>
/// Tab bar visibility policy for a page hosted by a <see cref="ScaffoldTabBar"/> area,
/// attached via <see cref="Scaffold.TabBarVisibilityProperty"/>.
/// </summary>
public enum ScaffoldTabBarVisibility
{
    /// <summary>The tab bar is visible (the default).</summary>
    Visible,

    /// <summary>
    /// The tab bar hides while the current navigation stack has pushed pages (more than one
    /// page) and shows again at stack roots. Hide/show animates in sync with the push/pop
    /// transition.
    /// </summary>
    Auto,

    /// <summary>The tab bar is hidden.</summary>
    Hidden
}
