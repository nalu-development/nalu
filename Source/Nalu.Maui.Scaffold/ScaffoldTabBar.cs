using Nalu.Internals;

namespace Nalu;

/// <summary>
/// A <see cref="ScaffoldArea"/> rendering a Nalu-drawn tab bar that switches between its roots.
/// </summary>
/// <remarks>
/// <para>
/// By default the tab bar auto-renders one tab per visible <see cref="ScaffoldRoot"/> from the
/// metadata quintet (<see cref="ScaffoldRoot.Title"/>, <see cref="ScaffoldRoot.Icon"/>,
/// <see cref="ScaffoldRoot.SelectedIcon"/>, <see cref="ScaffoldRoot.CurrentIcon"/>,
/// <see cref="ScaffoldRoot.IsSelected"/>) as a floating pill bar. Icons render untinted —
/// avatars work out of the box; a monochrome tinted look is expressed on the root's own
/// <see cref="ImageSource"/> (e.g. a <see cref="FontImageSource"/> color), never by the template.
/// </para>
/// <para>
/// Layout is driven by <see cref="ItemWidth"/>: the bar hugs its content and shows as many items
/// as fit the container width; the remaining roots move to an overflow panel behind a trailing
/// "More" item. A fully custom bar can be supplied via <see cref="TabBarView"/>.
/// Tab selection always routes through the Nalu navigation engine (guards apply);
/// tapping the active tab pops that root's stack back to the root page.
/// </para>
/// </remarks>
public class ScaffoldTabBar : ScaffoldArea
{
    private View? _defaultBarView;

    /// <summary>Bindable property for <see cref="TabBarView"/>.</summary>
    public static readonly BindableProperty TabBarViewProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<View?>(nameof(TabBarView));

    /// <summary>Bindable property for <see cref="TabBarScrimView"/>.</summary>
    public static readonly BindableProperty TabBarScrimViewProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<View?>(nameof(TabBarScrimView));

    #region Bar container properties

    /// <summary>Bindable property for <see cref="BarBackground"/>.</summary>
    public static readonly BindableProperty BarBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Brush?>(nameof(BarBackground));

    /// <summary>Bindable property for <see cref="BarCornerRadius"/>.</summary>
    public static readonly BindableProperty BarCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(BarCornerRadius), 26.0);

    /// <summary>Bindable property for <see cref="BarMargin"/>.</summary>
    public static readonly BindableProperty BarMarginProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(BarMargin), new Thickness(10, 0, 10, 10));

    /// <summary>Bindable property for <see cref="BarPadding"/>.</summary>
    public static readonly BindableProperty BarPaddingProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(BarPadding), new Thickness(6));

    /// <summary>Bindable property for <see cref="BarShadow"/>.</summary>
    public static readonly BindableProperty BarShadowProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Shadow?>(nameof(BarShadow));

    #endregion

    #region Item properties

    /// <summary>Bindable property for <see cref="ItemWidth"/>.</summary>
    public static readonly BindableProperty ItemWidthProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(ItemWidth), 76.0);

    /// <summary>Bindable property for <see cref="IconSize"/>.</summary>
    public static readonly BindableProperty IconSizeProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(IconSize), 26.0);

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Color?>(nameof(TextColor));

    /// <summary>Bindable property for <see cref="SelectedTextColor"/>.</summary>
    public static readonly BindableProperty SelectedTextColorProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Color?>(nameof(SelectedTextColor));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<string?>(nameof(FontFamily));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(FontSize), 11.0);

    /// <summary>Bindable property for <see cref="SelectionPillBackground"/>.</summary>
    public static readonly BindableProperty SelectionPillBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Brush?>(nameof(SelectionPillBackground));

    /// <summary>Bindable property for <see cref="SelectionPillCornerRadius"/>.</summary>
    public static readonly BindableProperty SelectionPillCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(SelectionPillCornerRadius), 20.0);

    #endregion

    #region Badge properties

    /// <summary>
    /// Attached property holding the badge text displayed on a <see cref="ScaffoldRoot"/>'s tab
    /// item (and its overflow row). Null or empty hides the badge.
    /// </summary>
    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.CreateAttached("BadgeText", typeof(string), typeof(ScaffoldTabBar), null);

    /// <summary>Bindable property for <see cref="BadgeBackground"/>.</summary>
    public static readonly BindableProperty BadgeBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Brush?>(nameof(BadgeBackground));

    /// <summary>Bindable property for <see cref="BadgeTextColor"/>.</summary>
    public static readonly BindableProperty BadgeTextColorProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Color?>(nameof(BadgeTextColor));

    /// <summary>Bindable property for <see cref="BadgeFontSize"/>.</summary>
    public static readonly BindableProperty BadgeFontSizeProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(BadgeFontSize), 11.0);

    #endregion

    #region Overflow properties

    /// <summary>Bindable property for <see cref="OverflowIcon"/>.</summary>
    public static readonly BindableProperty OverflowIconProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<ImageSource?>(nameof(OverflowIcon));

    /// <summary>Bindable property for <see cref="OverflowTitle"/>.</summary>
    public static readonly BindableProperty OverflowTitleProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<string?>(nameof(OverflowTitle), "More");

    /// <summary>Bindable property for <see cref="ScrimColor"/>.</summary>
    public static readonly BindableProperty ScrimColorProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Color?>(nameof(ScrimColor));

    /// <summary>Bindable property for <see cref="OverflowPanelBackground"/>.</summary>
    public static readonly BindableProperty OverflowPanelBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Brush?>(nameof(OverflowPanelBackground));

    /// <summary>Bindable property for <see cref="OverflowPanelCornerRadius"/>.</summary>
    public static readonly BindableProperty OverflowPanelCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create(nameof(OverflowPanelCornerRadius), 22.0);

    /// <summary>Bindable property for <see cref="OverflowPanelShadow"/>.</summary>
    public static readonly BindableProperty OverflowPanelShadowProperty =
        GenericBindableProperty<ScaffoldTabBar>.Create<Shadow?>(nameof(OverflowPanelShadow));

    #endregion

    /// <summary>
    /// Gets or sets a custom tab bar view replacing the default Nalu-drawn one.
    /// The view's binding context is this <see cref="ScaffoldTabBar"/> (exposing the roots and
    /// their selection state); call <see cref="SelectRootAsync"/> to trigger tab selection.
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

    /// <summary>Gets or sets the background brush of the floating bar pill. Defaults to a translucent theme-aware surface.</summary>
    public Brush? BarBackground
    {
        get => (Brush?)GetValue(BarBackgroundProperty);
        set => SetValue(BarBackgroundProperty, value);
    }

    /// <summary>Gets or sets the corner radius of the floating bar pill.</summary>
    public double BarCornerRadius
    {
        get => (double)GetValue(BarCornerRadiusProperty);
        set => SetValue(BarCornerRadiusProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin around the floating bar pill, relative to the safe area
    /// (the bottom margin is measured from the top of the system inset). Part of the bar's
    /// safe-area footprint contribution to the hosted page.
    /// </summary>
    public Thickness BarMargin
    {
        get => (Thickness)GetValue(BarMarginProperty);
        set => SetValue(BarMarginProperty, value);
    }

    /// <summary>Gets or sets the padding inside the floating bar pill.</summary>
    public Thickness BarPadding
    {
        get => (Thickness)GetValue(BarPaddingProperty);
        set => SetValue(BarPaddingProperty, value);
    }

    /// <summary>Gets or sets the shadow of the floating bar pill. Defaults to a subtle drop shadow.</summary>
    public Shadow? BarShadow
    {
        get => (Shadow?)GetValue(BarShadowProperty);
        set => SetValue(BarShadowProperty, value);
    }

    /// <summary>
    /// Gets or sets the fixed width of one tab item. This is the single layout input:
    /// as many items as fit the container width are shown in the bar, the rest move to the
    /// overflow panel behind a trailing "More" item. The bar hugs its content
    /// (width = shown items × <see cref="ItemWidth"/> + padding), centered.
    /// </summary>
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>Gets or sets the icon size (both dimensions) of tab items.</summary>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Gets or sets the label color of unselected tab items.</summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the label color of the selected tab item. Defaults to the Nalu accent.</summary>
    public Color? SelectedTextColor
    {
        get => (Color?)GetValue(SelectedTextColorProperty);
        set => SetValue(SelectedTextColorProperty, value);
    }

    /// <summary>Gets or sets the font family of tab item labels.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the font size of tab item labels.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the background of the rounded highlight behind the selected item.</summary>
    public Brush? SelectionPillBackground
    {
        get => (Brush?)GetValue(SelectionPillBackgroundProperty);
        set => SetValue(SelectionPillBackgroundProperty, value);
    }

    /// <summary>Gets or sets the corner radius of the selection highlight.</summary>
    public double SelectionPillCornerRadius
    {
        get => (double)GetValue(SelectionPillCornerRadiusProperty);
        set => SetValue(SelectionPillCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the badge background. Defaults to the Nalu accent.</summary>
    public Brush? BadgeBackground
    {
        get => (Brush?)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    /// <summary>Gets or sets the badge text color.</summary>
    public Color? BadgeTextColor
    {
        get => (Color?)GetValue(BadgeTextColorProperty);
        set => SetValue(BadgeTextColorProperty, value);
    }

    /// <summary>Gets or sets the badge font size.</summary>
    public double BadgeFontSize
    {
        get => (double)GetValue(BadgeFontSizeProperty);
        set => SetValue(BadgeFontSizeProperty, value);
    }

    /// <summary>Gets or sets the icon of the trailing "More" item. A built-in ••• glyph is drawn when not set.</summary>
    public ImageSource? OverflowIcon
    {
        get => (ImageSource?)GetValue(OverflowIconProperty);
        set => SetValue(OverflowIconProperty, value);
    }

    /// <summary>Gets or sets the label of the trailing "More" item.</summary>
    public string? OverflowTitle
    {
        get => (string?)GetValue(OverflowTitleProperty);
        set => SetValue(OverflowTitleProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrim color shown behind the overflow panel. The scrim covers the page
    /// content only — never the tab bar, which stays interactive while the panel is open.
    /// </summary>
    public Color? ScrimColor
    {
        get => (Color?)GetValue(ScrimColorProperty);
        set => SetValue(ScrimColorProperty, value);
    }

    /// <summary>Gets or sets the background of the overflow panel.</summary>
    public Brush? OverflowPanelBackground
    {
        get => (Brush?)GetValue(OverflowPanelBackgroundProperty);
        set => SetValue(OverflowPanelBackgroundProperty, value);
    }

    /// <summary>Gets or sets the corner radius of the overflow panel.</summary>
    public double OverflowPanelCornerRadius
    {
        get => (double)GetValue(OverflowPanelCornerRadiusProperty);
        set => SetValue(OverflowPanelCornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the shadow of the overflow panel.</summary>
    public Shadow? OverflowPanelShadow
    {
        get => (Shadow?)GetValue(OverflowPanelShadowProperty);
        set => SetValue(OverflowPanelShadowProperty, value);
    }

    /// <summary>Gets the badge text attached to a root.</summary>
    public static string? GetBadgeText(BindableObject bindable) => (string?)bindable.GetValue(BadgeTextProperty);

    /// <summary>Sets the badge text attached to a root.</summary>
    public static void SetBadgeText(BindableObject bindable, string? value) => bindable.SetValue(BadgeTextProperty, value);

    private volatile int _navigating;

    /// <summary>
    /// Selects the given root through the Nalu navigation engine: switching to another root
    /// restores its preserved navigation stack; selecting the current root pops its stack back
    /// to the root page. Guards and lifecycle events always run — a guarded page can cancel the
    /// switch. Re-entrant calls while a selection is in flight are ignored.
    /// </summary>
    /// <param name="root">The root to select; must belong to this tab bar's <see cref="ScaffoldArea.Roots"/>.</param>
    /// <returns>True when the navigation was executed (even if a guard canceled it midway).</returns>
    public async Task<bool> SelectRootAsync(ScaffoldRoot root)
    {
        if (FindScaffold() is not { } scaffold)
        {
            return false;
        }

        if (Interlocked.CompareExchange(ref _navigating, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            return await scaffold.SelectRootAsync(root).ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Exchange(ref _navigating, 0);
        }
    }

    /// <summary>
    /// Gets the view the presenter mounts as the bar: the user-supplied <see cref="TabBarView"/>
    /// or the lazily created default template. The view is a logical child of this tab bar, so
    /// its BindingContext and resource resolution flow through the scaffold structure.
    /// </summary>
    internal View GetOrCreateBarView()
    {
        if (TabBarView is { } custom)
        {
            if (custom.Parent is null)
            {
                AddLogicalChild(custom);
                custom.BindingContext = this;
            }

            return custom;
        }

        _defaultBarView ??= new ScaffoldTabBarView(this);

        if (_defaultBarView.Parent is null)
        {
            AddLogicalChild(_defaultBarView);
        }

        return _defaultBarView;
    }

    /// <summary>
    /// Detaches the bar view from the element tree while the chrome is unmounted (hidden page,
    /// non-tab-bar area): the element tree reflects the actually-presented chrome.
    /// <see cref="GetOrCreateBarView"/> re-attaches on the next mount.
    /// </summary>
    internal void OnBarViewUnmounted()
    {
        if (_defaultBarView is { Parent: not null } defaultView)
        {
            RemoveLogicalChild(defaultView);
        }

        if (TabBarView is { Parent: not null } customView && ReferenceEquals(customView.Parent, this))
        {
            RemoveLogicalChild(customView);
        }
    }

    /// <summary>The default-template instance, when created (used by the overflow panel plumbing).</summary>
    internal ScaffoldTabBarView? DefaultBarView => _defaultBarView as ScaffoldTabBarView;

    /// <summary>
    /// Opens the overflow panel listing the roots that don't fit the bar. No-op when nothing
    /// overflows. Implemented by the platform presenter's overlay layer: the scrim covers the
    /// page content only — the tab bar stays interactive.
    /// </summary>
    internal Task OpenOverflowAsync()
        => FindScaffold() is { Presenter: { } presenter } && DefaultBarView is { OverflowRoots.Count: > 0 } barView
            ? presenter.OpenTabBarOverflowAsync(this, barView)
            : Task.CompletedTask;

    private Scaffold? FindScaffold()
    {
        Element? element = this;

        while (element is not null and not Scaffold)
        {
            element = element.Parent;
        }

        return element as Scaffold;
    }
}
