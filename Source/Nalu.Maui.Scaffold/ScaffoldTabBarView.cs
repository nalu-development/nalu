using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The default Nalu tab bar component: a floating pill bar rendering one item per visible
/// <see cref="ScaffoldRoot"/> from the metadata quintet, with fixed <see cref="ItemWidth"/>
/// slots and a trailing "More" item collecting the roots that don't fit (shown in a wrap-grid
/// overflow panel above the bar). Created automatically as the default value of
/// <see cref="ScaffoldTabBar.TabBarViewProperty"/> — style it (including with
/// <c>AppThemeBinding</c>) or replace it entirely with any custom view.
/// </summary>
/// <remarks>
/// All styling lives HERE, not on <see cref="ScaffoldTabBar"/>: an app replacing the bar with a
/// custom view carries none of the default template's surface. Icons render untinted (avatars
/// work; monochrome tinting is expressed on the root's own <see cref="ImageSource"/>); color
/// defaults are theme-aware Nalu blues applied as fallbacks — values set here always win.
/// The component resolves its <see cref="ScaffoldTabBar"/> from its logical parent when the
/// scaffold presents it.
/// </remarks>
public sealed class ScaffoldTabBarView : Grid
{
    private readonly Border _pill;
    private readonly ScaffoldTabBarItemsLayout _items;
    private ScaffoldTabBar? _tabBar;

    #region Bar container properties

    /// <summary>Bindable property for <see cref="BarBackground"/>.</summary>
    public static readonly BindableProperty BarBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Brush?>(nameof(BarBackground));

    /// <summary>Bindable property for <see cref="BarCornerRadius"/>.</summary>
    public static readonly BindableProperty BarCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(BarCornerRadius), 26.0);

    /// <summary>Bindable property for <see cref="BarMargin"/>.</summary>
    public static readonly BindableProperty BarMarginProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(BarMargin), new Thickness(10, 0, 10, 10));

    /// <summary>Bindable property for <see cref="BarPadding"/>.</summary>
    public static readonly BindableProperty BarPaddingProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(BarPadding), new Thickness(6));

    /// <summary>Bindable property for <see cref="BarShadow"/>.</summary>
    public static readonly BindableProperty BarShadowProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Shadow?>(nameof(BarShadow));

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

    #endregion

    #region Item properties

    /// <summary>Bindable property for <see cref="ItemWidth"/>.</summary>
    public static readonly BindableProperty ItemWidthProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(ItemWidth), 76.0);

    /// <summary>Bindable property for <see cref="IconSize"/>.</summary>
    public static readonly BindableProperty IconSizeProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(IconSize), 26.0);

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Color?>(nameof(TextColor));

    /// <summary>Bindable property for <see cref="SelectedTextColor"/>.</summary>
    public static readonly BindableProperty SelectedTextColorProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Color?>(nameof(SelectedTextColor));

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<string?>(nameof(FontFamily));

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(FontSize), 11.0);

    /// <summary>Bindable property for <see cref="SelectionPillBackground"/>.</summary>
    public static readonly BindableProperty SelectionPillBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Brush?>(nameof(SelectionPillBackground));

    /// <summary>Bindable property for <see cref="SelectionPillCornerRadius"/>.</summary>
    public static readonly BindableProperty SelectionPillCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(SelectionPillCornerRadius), 20.0);

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

    #endregion

    #region Badge properties

    /// <summary>
    /// Attached property holding the badge text displayed on a <see cref="ScaffoldRoot"/>'s tab
    /// item (and its overflow row). Null or empty hides the badge.
    /// </summary>
    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.CreateAttached("BadgeText", typeof(string), typeof(ScaffoldTabBarView), null);

    /// <summary>Bindable property for <see cref="BadgeBackground"/>.</summary>
    public static readonly BindableProperty BadgeBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Brush?>(nameof(BadgeBackground));

    /// <summary>Bindable property for <see cref="BadgeTextColor"/>.</summary>
    public static readonly BindableProperty BadgeTextColorProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Color?>(nameof(BadgeTextColor));

    /// <summary>Bindable property for <see cref="BadgeFontSize"/>.</summary>
    public static readonly BindableProperty BadgeFontSizeProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(BadgeFontSize), 11.0);

    /// <summary>Gets the badge text attached to a root.</summary>
    public static string? GetBadgeText(BindableObject bindable) => (string?)bindable.GetValue(BadgeTextProperty);

    /// <summary>Sets the badge text attached to a root.</summary>
    public static void SetBadgeText(BindableObject bindable, string? value) => bindable.SetValue(BadgeTextProperty, value);

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

    #endregion

    #region Overflow properties

    /// <summary>Bindable property for <see cref="OverflowIcon"/>.</summary>
    public static readonly BindableProperty OverflowIconProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<ImageSource?>(nameof(OverflowIcon));

    /// <summary>Bindable property for <see cref="OverflowTitle"/>.</summary>
    public static readonly BindableProperty OverflowTitleProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<string?>(nameof(OverflowTitle), "More");

    /// <summary>Bindable property for <see cref="ScrimColor"/>.</summary>
    public static readonly BindableProperty ScrimColorProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Color?>(nameof(ScrimColor));

    /// <summary>Bindable property for <see cref="OverflowPanelBackground"/>.</summary>
    public static readonly BindableProperty OverflowPanelBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Brush?>(nameof(OverflowPanelBackground));

    /// <summary>Bindable property for <see cref="OverflowPanelCornerRadius"/>.</summary>
    public static readonly BindableProperty OverflowPanelCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(nameof(OverflowPanelCornerRadius), 22.0);

    /// <summary>Bindable property for <see cref="OverflowPanelShadow"/>.</summary>
    public static readonly BindableProperty OverflowPanelShadowProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Shadow?>(nameof(OverflowPanelShadow));

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
    /// Gets or sets the scrim color shown behind the overflow panel. The scrim renders below
    /// the tab bar in z-order — the bar stays undimmed and interactive while the panel is open.
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

    #endregion

    /// <summary>The owning tab bar, resolved from the logical parent when the scaffold presents this view.</summary>
    internal ScaffoldTabBar? TabBar => _tabBar;

    /// <summary>Current effective styling; refreshed on theme and styling-property changes.</summary>
    internal ScaffoldTabBarStyleValues EffectiveStyle { get; private set; }

    /// <summary>Roots currently living in the overflow panel (recomputed by the items layout on measure).</summary>
    internal IReadOnlyList<ScaffoldRoot> OverflowRoots => _items.OverflowRoots;

    /// <summary>Raised when the overflow set changes (an open panel must close or refresh).</summary>
    internal event Action? OverflowRootsChanged;

    /// <summary>Initializes the default tab bar component. Item content builds once the view is parented to a <see cref="ScaffoldTabBar"/>.</summary>
    public ScaffoldTabBarView()
    {
        EffectiveStyle = ScaffoldTabBarPalette.Resolve(this);

        BackgroundColor = Colors.Transparent;

        // A star-row root Grid FILLS bounded measure constraints (the bar would measure
        // full-screen inside the platform strip) — the single row must be Auto.
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        _items = new ScaffoldTabBarItemsLayout(this);

        _pill = new Border
        {
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Content = _items
        };

        // The strip hosting the bar reserves the bottom system inset itself (on both
        // platforms) — the bar must never consume safe-area padding on top of that. This also
        // guards the Android hide/show slide: a translated strip overlaps the system bars and
        // the net10 inset listener would otherwise pad the bar by the overlap, and the stale
        // padding survived the slide back in (bar re-appearing ABOVE its resting position).
        SafeAreaEdges = SafeAreaEdges.None;

        Add(_pill);

        Padding = BarMargin;
        _pill.Padding = BarPadding;
        ApplyStyling();

        if (Application.Current is { } application)
        {
            // The default bar lives as long as its ScaffoldTabBar (static structure): no unsubscription needed.
            application.RequestedThemeChanged += (_, _) => ApplyStyling();
        }
    }

    internal Task OnItemTappedAsync(ScaffoldRoot? root)
        => _tabBar is null ? Task.CompletedTask
            : root is null ? _tabBar.OpenOverflowAsync()
            : _tabBar.SelectRootAsync(root);

    internal void NotifyOverflowRootsChanged() => OverflowRootsChanged?.Invoke();

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is ScaffoldTabBar tabBar && !ReferenceEquals(_tabBar, tabBar))
        {
            _tabBar = tabBar;
            _items.Rebuild();
            ApplyStyling();

            if (tabBar.Roots is INotifyCollectionChanged observableRoots)
            {
                observableRoots.CollectionChanged += (_, _) => _items.Rebuild();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        switch (propertyName)
        {
            case nameof(BarMargin):
                Padding = BarMargin;

                break;

            case nameof(BarPadding):
                _pill.Padding = BarPadding;

                break;

            case nameof(BarBackground):
            case nameof(BarShadow):
            case nameof(BarCornerRadius):
            case nameof(TextColor):
            case nameof(SelectedTextColor):
            case nameof(SelectionPillBackground):
            case nameof(SelectionPillCornerRadius):
            case nameof(BadgeBackground):
            case nameof(BadgeTextColor):
            case nameof(BadgeFontSize):
            case nameof(FontFamily):
            case nameof(FontSize):
            case nameof(IconSize):
            case nameof(OverflowIcon):
            case nameof(OverflowTitle):
                ApplyStyling();

                break;

            case nameof(ItemWidth):
                _items.InvalidateMeasure();

                break;
        }
    }

    private void ApplyStyling()
    {
        EffectiveStyle = ScaffoldTabBarPalette.Resolve(this);

        _pill.Background = EffectiveStyle.BarBackground;
        _pill.Shadow = EffectiveStyle.BarShadow;
        _pill.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(BarCornerRadius) };

        foreach (var item in _items.ItemViews)
        {
            item.ApplyStyling();
        }
    }
}
