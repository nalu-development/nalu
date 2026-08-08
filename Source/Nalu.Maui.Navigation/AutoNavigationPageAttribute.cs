namespace Nalu;

/// <summary>
/// Tunes how the Nalu navigation source generator registers a <see cref="Page"/> class
/// (the generated <c>AddPages()</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoNavigationPageAttribute : Attribute
{
    /// <summary>
    /// Whether the automatic registration is enabled for this page. Defaults to true.
    /// When false the page is skipped by the generated <c>AddPages()</c>: it is not registered
    /// as a navigation target nor added to the service collection. Use it for pages that are
    /// never navigated to through Nalu navigation (e.g. hand-managed dialogs). A page excluded
    /// here can still be registered manually via
    /// <see cref="NavigationConfigurator.AddPage{TPage}()"/> and friends.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
