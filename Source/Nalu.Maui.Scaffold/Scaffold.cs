using System.Diagnostics.CodeAnalysis;

namespace Nalu;

/// <summary>
/// Mobile-only (iOS/Android) application root replacing MAUI <see cref="Shell"/>,
/// driven by Nalu.Maui.Navigation.
/// </summary>
/// <remarks>
/// <para>
/// The scaffold owns the whole chrome (navigation bar, tab bar, flyouts) as Nalu-drawn virtual
/// views, hosts one navigation stack per <see cref="ScaffoldRoot"/>, and implements the
/// navigation host contracts so the standard Nalu fluent navigation API
/// (<c>Navigation.Relative()</c> / <c>Navigation.Absolute()</c>) drives it unchanged.
/// </para>
/// <para>
/// Structure: <see cref="Scaffold"/> → <see cref="ScaffoldArea"/> (or its
/// <see cref="ScaffoldTabBar"/> specialization) → <see cref="ScaffoldRoot"/>, each root hosting
/// an independent navigation stack. There are no developer-facing routes: absolute navigation is
/// type-based and resolves its destination from the registered root page types.
/// </para>
/// <code>
/// <![CDATA[
/// <nalu:Scaffold>
///     <nalu:ScaffoldTabBar>
///         <nalu:ScaffoldRoot Title="Home" Icon="home.png" PageType="pages:FeedPage" />
///         <nalu:ScaffoldRoot Title="Search" Icon="search.png" PageType="pages:SearchPage" />
///     </nalu:ScaffoldTabBar>
///     <!-- Terse form: implicitly wrapped into a single-root ScaffoldArea at parse time. -->
///     <nalu:ScaffoldRoot PageType="pages:SettingsPage" />
/// </nalu:Scaffold>
/// ]]>
/// </code>
/// </remarks>
[ContentProperty(nameof(Areas))]
public class Scaffold : Page
{
    private static readonly BindablePropertyKey _currentAreaPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentArea), typeof(ScaffoldArea), typeof(Scaffold), null);

    /// <summary>Bindable property for <see cref="CurrentArea"/> (read-only).</summary>
    public static readonly BindableProperty CurrentAreaProperty = _currentAreaPropertyKey.BindableProperty;

    /// <summary>
    /// Attached property holding the start-edge (leading) flyout content.
    /// Resolution order, most specific wins: current <see cref="Page"/> →
    /// current <see cref="ScaffoldArea"/> → the <see cref="Scaffold"/> itself (global).
    /// No value at any level means the drawer does not exist.
    /// </summary>
    public static readonly BindableProperty FlyoutStartProperty =
        BindableProperty.CreateAttached(nameof(FlyoutStart), typeof(View), typeof(Scaffold), null);

    /// <summary>
    /// Attached property holding the end-edge (trailing) flyout content.
    /// Same resolution rules as <see cref="FlyoutStartProperty"/>.
    /// </summary>
    public static readonly BindableProperty FlyoutEndProperty =
        BindableProperty.CreateAttached(nameof(FlyoutEnd), typeof(View), typeof(Scaffold), null);

    /// <summary>
    /// Attached property replacing the navigation bar title view for a <see cref="Page"/>.
    /// The page's <see cref="Page.Title"/> is used when no title view is set.
    /// </summary>
    public static readonly BindableProperty TitleViewProperty =
        BindableProperty.CreateAttached("TitleView", typeof(View), typeof(Scaffold), null);

    /// <summary>
    /// Attached property controlling navigation bar visibility for a <see cref="Page"/>.
    /// Toggling visibility is a safe-area inset change, not a page relayout.
    /// </summary>
    public static readonly BindableProperty NavBarVisibleProperty =
        BindableProperty.CreateAttached("NavBarVisible", typeof(bool), typeof(Scaffold), true);

    /// <summary>
    /// Attached property controlling tab bar visibility for a <see cref="Page"/>.
    /// Toggling visibility is a safe-area inset change, not a page relayout.
    /// </summary>
    public static readonly BindableProperty TabBarVisibleProperty =
        BindableProperty.CreateAttached("TabBarVisible", typeof(bool), typeof(Scaffold), true);

    /// <summary>
    /// Gets or sets the page type of the root to open at startup. Optional: when not set, the
    /// first root of the first area is opened. Must match the <see cref="ScaffoldRoot.PageType"/>
    /// of one of the scaffold's roots.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? InitialRootPageType { get; set; }

    /// <summary>Gets the destination areas composing the application structure.</summary>
    public IList<ScaffoldArea> Areas { get; }

    /// <summary>
    /// Gets the selected area (defaults to the first one). Read-only: selection changes only
    /// through the Nalu navigation engine (absolute navigation or flyout interaction), so
    /// lifecycle events and leaving guards can never be bypassed. Observable via binding.
    /// </summary>
    public ScaffoldArea? CurrentArea
    {
        get => (ScaffoldArea?)GetValue(CurrentAreaProperty);
        internal set => SetValue(_currentAreaPropertyKey, value);
    }

    /// <summary>Gets or sets the global start-edge flyout content (see <see cref="FlyoutStartProperty"/>).</summary>
    public View? FlyoutStart
    {
        get => (View?)GetValue(FlyoutStartProperty);
        set => SetValue(FlyoutStartProperty, value);
    }

    /// <summary>Gets or sets the global end-edge flyout content (see <see cref="FlyoutEndProperty"/>).</summary>
    public View? FlyoutEnd
    {
        get => (View?)GetValue(FlyoutEndProperty);
        set => SetValue(FlyoutEndProperty, value);
    }

    /// <summary>Initializes a new <see cref="Scaffold"/>.</summary>
    public Scaffold()
    {
        Areas = new ScaffoldElementCollection<ScaffoldArea>(this);
    }

    /// <summary>Gets the start-edge flyout content attached to an element.</summary>
    public static View? GetFlyoutStart(BindableObject bindable) => (View?)bindable.GetValue(FlyoutStartProperty);

    /// <summary>Sets the start-edge flyout content attached to an element.</summary>
    public static void SetFlyoutStart(BindableObject bindable, View? value) => bindable.SetValue(FlyoutStartProperty, value);

    /// <summary>Gets the end-edge flyout content attached to an element.</summary>
    public static View? GetFlyoutEnd(BindableObject bindable) => (View?)bindable.GetValue(FlyoutEndProperty);

    /// <summary>Sets the end-edge flyout content attached to an element.</summary>
    public static void SetFlyoutEnd(BindableObject bindable, View? value) => bindable.SetValue(FlyoutEndProperty, value);

    /// <summary>Gets the navigation bar title view attached to a page.</summary>
    public static View? GetTitleView(BindableObject bindable) => (View?)bindable.GetValue(TitleViewProperty);

    /// <summary>Sets the navigation bar title view attached to a page.</summary>
    public static void SetTitleView(BindableObject bindable, View? value) => bindable.SetValue(TitleViewProperty, value);

    /// <summary>Gets whether the navigation bar is visible for a page.</summary>
    public static bool GetNavBarVisible(BindableObject bindable) => (bool)bindable.GetValue(NavBarVisibleProperty);

    /// <summary>Sets whether the navigation bar is visible for a page.</summary>
    public static void SetNavBarVisible(BindableObject bindable, bool value) => bindable.SetValue(NavBarVisibleProperty, value);

    /// <summary>Gets whether the tab bar is visible for a page.</summary>
    public static bool GetTabBarVisible(BindableObject bindable) => (bool)bindable.GetValue(TabBarVisibleProperty);

    /// <summary>Sets whether the tab bar is visible for a page.</summary>
    public static void SetTabBarVisible(BindableObject bindable, bool value) => bindable.SetValue(TabBarVisibleProperty, value);
}
