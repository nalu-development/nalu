namespace Nalu;

/// <summary>
/// Tunes what the Nalu navigation source generator registers (the generated <c>AddPages()</c>),
/// with direction depending on the decorated class:
/// <list type="bullet">
/// <item>
/// On a <see cref="ContentPage"/>-derived class — an opt-OUT knob: pages are discovered
/// automatically, and <c>Enabled = false</c> excludes one.
/// </item>
/// <item>
/// On any other class — an opt-IN marker: the class is registered as a component-based page
/// (model-less <c>AddPage&lt;TComponent&gt;()</c>, rendered through the registered
/// <see cref="IComponentPageFactory"/>). Decorate only components whose <c>Render()</c>
/// produces a Page-rooted tree — plain view components are not navigation destinations, and
/// page-ness cannot be inferred statically. Typed intents implemented by the component feed
/// the generated <c>AddIntents()</c> like any page's.
/// </item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoNavigationPageAttribute : Attribute
{
    /// <summary>
    /// Whether the automatic registration is enabled for this class. Defaults to true.
    /// When false the class is skipped by the generated <c>AddPages()</c>: it is not registered
    /// as a navigation target nor added to the service collection. Use it for pages that are
    /// never navigated to through Nalu navigation (e.g. hand-managed dialogs). A class excluded
    /// here can still be registered manually via
    /// <see cref="NavigationConfigurator.AddPage{TPage}()"/> and friends.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
