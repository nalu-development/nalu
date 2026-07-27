using System.Collections.Specialized;
using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The default Nalu tab bar component: a floating pill bar rendering one
/// <see cref="ScaffoldTabBarItemView"/> per visible <see cref="ScaffoldRoot"/> from the metadata
/// quintet, with fixed <see cref="ItemWidth"/> slots and a trailing "More" item collecting the
/// roots that don't fit (shown in a <see cref="ScaffoldTabBarOverflowView"/> above the bar).
/// Created automatically as the default value of <see cref="ScaffoldTabBar.TabBarViewProperty"/> —
/// style it or replace it entirely with any custom view.
/// </summary>
/// <remarks>
/// <para>
/// The template splits into one component per styling concern, each carrying real default
/// values overridden by plain implicit styles: this class owns the BAR itself (pill container +
/// layout input + "More" content), <see cref="ScaffoldTabBarItemView"/> owns item and badge
/// appearance (bar items and overflow rows alike), <see cref="ScaffoldTabBarOverflowView"/>
/// owns the overflow panel surface. Theming is ordinary MAUI:
/// <code>
/// &lt;Style TargetType="nalu:ScaffoldTabBarView"&gt;
///     &lt;Setter Property="BarBackground" Value="{AppThemeBinding Light=..., Dark=...}" /&gt;
/// &lt;/Style&gt;
/// &lt;Style TargetType="nalu:ScaffoldTabBarItemView"&gt;
///     &lt;Setter Property="SelectedTextColor" Value="{StaticResource Accent}" /&gt;
/// &lt;/Style&gt;
/// </code>
/// </para>
/// <para>
/// Icons render untinted (avatars work; monochrome tinting is expressed on the root's own
/// <see cref="ImageSource"/>). The component resolves its <see cref="ScaffoldTabBar"/> from its
/// logical parent when the scaffold presents it.
/// </para>
/// </remarks>
public sealed class ScaffoldTabBarView : Grid
{
    private readonly Border _pill;
    private readonly ScaffoldTabBarItemsLayout _items;
    private ScaffoldTabBar? _tabBar;

    // Null-conditionals in the callbacks below: implicit styles apply from the VisualElement
    // base ctor, before _pill/_items exist; the ctor seeds the values.

    #region Bar container properties

    /// <summary>Bindable property for <see cref="BarBackground"/>.</summary>
    public static readonly BindableProperty BarBackgroundProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Brush?>(
            nameof(BarBackground),
            defaultValueCreator: static _ => new SolidColorBrush(Color.FromArgb("#F2FFFFFF")),
            propertyChanged: static view => (_, value) => view._pill?.Background = value
        );

    /// <summary>Bindable property for <see cref="BarCornerRadius"/>.</summary>
    public static readonly BindableProperty BarCornerRadiusProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(
            nameof(BarCornerRadius),
            26.0,
            propertyChanged: static view => (_, value) => view._pill?.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(value) }
        );

    /// <summary>Bindable property for <see cref="BarMargin"/>.</summary>
    public static readonly BindableProperty BarMarginProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(
            nameof(BarMargin),
            new Thickness(10, 0, 10, 10),
            propertyChanged: static view => (_, value) => view.Padding = value
        );

    /// <summary>Bindable property for <see cref="BarPadding"/>.</summary>
    public static readonly BindableProperty BarPaddingProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(
            nameof(BarPadding),
            new Thickness(6),
            propertyChanged: static view => (_, value) => view._pill?.Padding = value
        );

    /// <summary>Bindable property for <see cref="BarShadow"/>.</summary>
    public static readonly BindableProperty BarShadowProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<Shadow>(
            nameof(BarShadow),
            defaultValueCreator: static _ => new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.18f,
                Radius = 14,
                Offset = new Point(0, 3)
            },
            propertyChanged: static view => (_, value) => view._pill?.Shadow = value
        );

    /// <summary>Gets or sets the background brush of the floating bar pill.</summary>
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
    /// safe-area footprint contribution to the hosted page. Drives the view's own
    /// <see cref="Layout.Padding"/> — style THIS, not <c>Padding</c>.
    /// </summary>
    public Thickness BarMargin
    {
        get => (Thickness)GetValue(BarMarginProperty);
        set => SetValue(BarMarginProperty, value);
    }

    /// <summary>Gets or sets the padding inside the floating bar pill (mirrored by the overflow panel).</summary>
    public Thickness BarPadding
    {
        get => (Thickness)GetValue(BarPaddingProperty);
        set => SetValue(BarPaddingProperty, value);
    }

    /// <summary>Gets or sets the shadow of the floating bar pill.</summary>
    public Shadow BarShadow
    {
        get => (Shadow)GetValue(BarShadowProperty);
        set => SetValue(BarShadowProperty, value);
    }

    #endregion

    #region Layout and overflow content properties

    /// <summary>Bindable property for <see cref="ItemWidth"/>.</summary>
    public static readonly BindableProperty ItemWidthProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create(
            nameof(ItemWidth),
            76.0,
            propertyChanged: static view => (_, _) => view._items?.InvalidateMeasure()
        );

    /// <summary>Bindable property for <see cref="OverflowIcon"/>.</summary>
    public static readonly BindableProperty OverflowIconProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<ImageSource?>(
            nameof(OverflowIcon),
            // The More item builds either an Image or the drawn ••• glyph at construction:
            // swapping the icon swaps the item's content, hence a rebuild (rare by nature).
            propertyChanged: static view => (_, _) => view._items?.RebuildMoreItem()
        );

    /// <summary>Bindable property for <see cref="OverflowTitle"/>.</summary>
    public static readonly BindableProperty OverflowTitleProperty =
        GenericBindableProperty<ScaffoldTabBarView>.Create<string?>(nameof(OverflowTitle), "More");

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

    #endregion

    /// <summary>
    /// Attached property holding the badge text displayed on a <see cref="ScaffoldRoot"/>'s tab
    /// item (and its overflow row). Null or empty hides the badge.
    /// </summary>
    public static readonly BindableProperty BadgeTextProperty =
        BindableProperty.CreateAttached("BadgeText", typeof(string), typeof(ScaffoldTabBarView), null);

    /// <summary>Gets the badge text attached to a root.</summary>
    public static string? GetBadgeText(BindableObject bindable) => (string?)bindable.GetValue(BadgeTextProperty);

    /// <summary>Sets the badge text attached to a root.</summary>
    public static void SetBadgeText(BindableObject bindable, string? value) => bindable.SetValue(BadgeTextProperty, value);

    /// <summary>The owning tab bar, resolved from the logical parent when the scaffold presents this view.</summary>
    internal ScaffoldTabBar? TabBar => _tabBar;

    /// <summary>Roots currently living in the overflow panel (recomputed by the items layout on measure).</summary>
    internal IReadOnlyList<ScaffoldRoot> OverflowRoots => _items.OverflowRoots;

    /// <summary>Raised when the overflow set changes (an open panel must close or refresh).</summary>
    internal event Action? OverflowRootsChanged;

    /// <summary>Initializes the default tab bar component. Item content builds once the view is parented to a <see cref="ScaffoldTabBar"/>.</summary>
    public ScaffoldTabBarView()
    {
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

        // Defaults never raise propertyChanged: seed the pill from the current values once.
        // Every later change lands through that property's own callback.
        Padding = BarMargin;
        _pill.Padding = BarPadding;
        _pill.Background = BarBackground;
        _pill.Shadow = BarShadow;
        _pill.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(BarCornerRadius) };
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

            if (tabBar.Roots is INotifyCollectionChanged observableRoots)
            {
                observableRoots.CollectionChanged += (_, _) => _items.Rebuild();
            }
        }
    }
}
