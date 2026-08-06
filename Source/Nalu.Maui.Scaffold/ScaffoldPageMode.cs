namespace Nalu;

/// <summary>
/// Presentation mode of a page hosted by the <see cref="Scaffold"/>, attached via
/// <see cref="Scaffold.PageModeProperty"/>.
/// </summary>
public enum ScaffoldPageMode
{
    /// <summary>A regular navigation page.</summary>
    Default,

    /// <summary>
    /// Modal presentation (§7.1): enters from the bottom by default (an explicit page-attached
    /// <see cref="Scaffold.PageTransitionProperty"/> still wins), covers the tab bar, gets no
    /// interactive back preview (iOS edge swipe, Android predictive back — Android system back
    /// still commits through the engine, where <see cref="ILeavingGuard"/> decides), and the
    /// default nav bar shows the title only (no back chevron, no drawer buttons).
    /// Dismissal is programmatic.
    /// </summary>
    Modal,

    /// <summary>
    /// <see cref="Modal"/> plus a trailing close (X) button on the default nav bar, popping the
    /// page through the navigation engine (guards and lifecycle run).
    /// </summary>
    DismissableModal
}
