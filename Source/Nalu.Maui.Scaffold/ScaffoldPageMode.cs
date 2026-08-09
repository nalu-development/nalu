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
    /// <see cref="Scaffold.PageTransitionProperty"/> still wins), covers the tab bar, and the
    /// default nav bar shows the title only (no back chevron, no drawer buttons).
    /// System back is blocked entirely — the iOS edge swipe and the Android predictive back get
    /// no preview, and the Android back press is consumed without popping. Dismissal is
    /// programmatic only (engine pops still run <see cref="ILeavingGuard"/> and lifecycle).
    /// </summary>
    Modal,

    /// <summary>
    /// <see cref="Modal"/> plus dismissal affordances: a trailing close (X) button on the
    /// default nav bar, and the Android system back pops again — both route through the
    /// navigation engine (guards and lifecycle run). The interactive previews stay disabled.
    /// </summary>
    DismissableModal
}
