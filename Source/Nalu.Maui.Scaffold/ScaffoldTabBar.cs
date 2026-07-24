using Nalu.Internals;

namespace Nalu;

/// <summary>
/// A <see cref="ScaffoldArea"/> rendering a Nalu-drawn tab bar that switches between its roots.
/// </summary>
/// <remarks>
/// By default the tab bar auto-renders one tab per <see cref="ScaffoldRoot"/> from its
/// <see cref="ScaffoldRoot.Title"/> and <see cref="ScaffoldRoot.Icon"/>.
/// A fully custom bar can be supplied via <see cref="TabBarView"/>.
/// Tab selection always routes through the Nalu navigation engine (guards apply);
/// tapping the active tab pops that root's stack back to the root page.
/// </remarks>
public class ScaffoldTabBar : ScaffoldArea
{
    /// <summary>Bindable property for <see cref="TabBarView"/>.</summary>
    public static readonly BindableProperty TabBarViewProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<View?>(nameof(TabBarView));

    /// <summary>Bindable property for <see cref="TabBarScrimView"/>.</summary>
    public static readonly BindableProperty TabBarScrimViewProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<View?>(nameof(TabBarScrimView));

    /// <summary>
    /// Gets or sets a custom tab bar view replacing the default Nalu-drawn one.
    /// The view's binding context exposes the roots, the selection state and a select command.
    /// </summary>
    public View? TabBarView
    {
        get => (View?)GetValue(TabBarViewProperty);
        set => SetValue(TabBarViewProperty, value);
    }

    /// <summary>
    /// Gets or sets a scrim view rendered edge-to-edge behind the tab bar
    /// (gradients/blur backdrops extending into the bottom safe area).
    /// </summary>
    public View? TabBarScrimView
    {
        get => (View?)GetValue(TabBarScrimViewProperty);
        set => SetValue(TabBarScrimViewProperty, value);
    }
}
