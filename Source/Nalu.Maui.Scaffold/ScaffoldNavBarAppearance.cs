namespace Nalu;

/// <summary>
/// Per-page presentation of the nav bar strip, attached via
/// <see cref="Scaffold.NavBarAppearanceProperty"/>. Every property is optional: the effective
/// value of each one resolves independently through the current <see cref="Page"/> → current
/// <see cref="ScaffoldArea"/> → <see cref="Scaffold"/> chain, falling back to the built-in
/// defaults — a page-level appearance is a DELTA over the global one, not a replacement.
/// </summary>
/// <remarks>
/// <para>
/// Values apply to the strip hosting the mounted bar view (default or custom alike) and never
/// write into any view's own bindable properties — styles on the bar and its primitives keep
/// working. <see cref="Foreground"/> and <see cref="TitleForeground"/> are the values flowing
/// further: they reach the primitives through <see cref="ScaffoldNavBarContext.Foreground"/> /
/// <see cref="ScaffoldNavBarContext.TitleForeground"/> as their color fallback.
/// </para>
/// <para>
/// The object is live: its properties are bindable (it inherits the binding context of the
/// element it is attached to) and mutations apply immediately — bind or animate
/// <see cref="Opacity"/>, <see cref="OffsetY"/> or a <see cref="SolidColorBrush.Color"/>
/// inside <see cref="Background"/> for scroll-driven chrome. An appearance instance declared
/// in a shared <see cref="Style"/> is one object attached to many elements: fine for constant
/// values, unsuitable for bindings.
/// </para>
/// <para>
/// It is an <see cref="Element"/> parented to the element it is attached to (and re-rooted on the
/// element presenting it whenever it enters the resolution chain): that is what lets
/// <c>{AppThemeBinding}</c> and <c>{DynamicResource}</c> on its properties — and inside its
/// <see cref="Background"/> brush, parented to the appearance in turn — keep following the app
/// theme and resources, even while another appearance is the one presented. MAUI delivers those
/// changes down the element tree only.
/// </para>
/// </remarks>
public sealed class ScaffoldNavBarAppearance : Element
{
    /// <summary>The default strip background used when no appearance in the chain sets one.</summary>
    internal static readonly Color _defaultBackgroundColor = Color.FromArgb("#F7FFFFFF");

    /// <summary>Bindable property for <see cref="Background"/>.</summary>
    public static readonly BindableProperty BackgroundProperty =
        BindableProperty.Create(nameof(Background), typeof(Brush), typeof(ScaffoldNavBarAppearance), propertyChanged: OnBackgroundChanged);

    // The brush joins the appearance's element chain so theme/resource changes reach it whether or
    // not it is the brush currently painted on the strip (MAUI's own Background tracking only
    // notifies the brush while it is applied — and does not refresh it when re-applied). MAUI's
    // shared immutable brushes (Brush.Transparent & co.) are never re-parented.
    private static void OnBackgroundChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var appearance = (ScaffoldNavBarAppearance) bindable;

        if (oldValue is Brush old && ReferenceEquals(old.Parent, appearance))
        {
            old.Parent = null;
        }

        if (newValue is Brush brush && brush.Parent is null && brush.GetType().Name != "ImmutableBrush")
        {
            brush.Parent = appearance;
        }
    }

    /// <summary>
    /// Makes sure this appearance sits in a LIVE element chain: parented to <paramref name="owner"/>
    /// unless it already hangs from an element that reaches the <see cref="Application"/> (an
    /// appearance shared by a style may still be parented to a page that has since been
    /// destroyed). Re-parenting also refreshes its dynamic resources — theme included.
    /// </summary>
    internal void EnsureRooted(Element owner)
    {
        if (ReferenceEquals(Parent, owner) || IsRooted(Parent))
        {
            return;
        }

        Parent = owner;
    }

    private static bool IsRooted(Element? element)
    {
        while (element is not null)
        {
            if (element is Application)
            {
                return true;
            }

            element = element.Parent;
        }

        return false;
    }

    /// <summary>Bindable property for <see cref="Foreground"/>.</summary>
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(Color), typeof(ScaffoldNavBarAppearance));

    /// <summary>Bindable property for <see cref="TitleForeground"/>.</summary>
    public static readonly BindableProperty TitleForegroundProperty =
        BindableProperty.Create(nameof(TitleForeground), typeof(Color), typeof(ScaffoldNavBarAppearance));

    /// <summary>Bindable property for <see cref="Opacity"/>.</summary>
    public static readonly BindableProperty OpacityProperty =
        BindableProperty.Create(nameof(Opacity), typeof(double), typeof(ScaffoldNavBarAppearance), 1.0);

    /// <summary>Bindable property for <see cref="OffsetY"/>.</summary>
    public static readonly BindableProperty OffsetYProperty =
        BindableProperty.Create(nameof(OffsetY), typeof(double), typeof(ScaffoldNavBarAppearance), 0.0);

    /// <summary>Gets or sets the strip background (extends under the status bar).</summary>
    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the fallback tint of the nav bar primitives (title text and glyphs).
    /// A color set directly (or via style) on a primitive always wins over this; the title
    /// prefers <see cref="TitleForeground"/> when one is set in the chain.
    /// </summary>
    public Color? Foreground
    {
        get => (Color?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the fallback color of the title text only (<see cref="ScaffoldNavBarTitle"/>),
    /// letting the title and the bar buttons carry different colors while both still follow the
    /// page → area → scaffold appearance chain. Resolved level by level together with
    /// <see cref="Foreground"/>: the first appearance setting either wins, and its
    /// <see cref="TitleForeground"/> beats its <see cref="Foreground"/> — so a page setting only
    /// <see cref="Foreground"/> recolors the title too, even when the scaffold sets a title color.
    /// </summary>
    public Color? TitleForeground
    {
        get => (Color?)GetValue(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }

    /// <summary>Gets or sets the opacity of the whole strip content (background included).</summary>
    public double Opacity
    {
        get => (double)GetValue(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>Gets or sets the vertical translation of the whole strip content, in dp.</summary>
    public double OffsetY
    {
        get => (double)GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    private static readonly BindablePropertyKey _contextPropertyKey =
        BindableProperty.CreateReadOnly(nameof(Context), typeof(ScaffoldNavBarContext), typeof(ScaffoldNavBarAppearance), null);

    /// <summary>Bindable property for <see cref="Context"/> (read-only).</summary>
    public static readonly BindableProperty ContextProperty = _contextPropertyKey.BindableProperty;

    /// <summary>
    /// Gets the ambient <see cref="ScaffoldNavBarContext"/> while this appearance is part of
    /// the presented resolution chain (null otherwise). Appearance objects live OUTSIDE the
    /// visual tree, so ancestor-based bindings can't reach the context from here — this stamp
    /// is what lets <see cref="ScrollValueExtension"/>/<see cref="ThemeScrollValueExtension"/>
    /// bind scroll-driven values on appearance properties.
    /// </summary>
    public ScaffoldNavBarContext? Context => (ScaffoldNavBarContext?)GetValue(ContextProperty);

    internal void SetContext(ScaffoldNavBarContext? context) => SetValue(_contextPropertyKey, context);

    /// <summary>
    /// Resolves the effective title color: level by level (page → area → scaffold), the first
    /// appearance that sets <see cref="TitleForeground"/> OR <see cref="Foreground"/> wins, its
    /// <see cref="TitleForeground"/> taking precedence over its <see cref="Foreground"/>. A page
    /// declaring only <c>Foreground="White"</c> therefore recolors the whole bar even when the
    /// scaffold gives the title its own color; a page wanting the two apart sets both.
    /// </summary>
    internal static Color? ResolveTitleForeground(
        ScaffoldNavBarAppearance? page,
        ScaffoldNavBarAppearance? area,
        ScaffoldNavBarAppearance? scaffold
    )
    {
        foreach (var appearance in (ReadOnlySpan<ScaffoldNavBarAppearance?>)[page, area, scaffold])
        {
            if (appearance is null)
            {
                continue;
            }

            if (appearance.IsSet(TitleForegroundProperty))
            {
                return appearance.TitleForeground;
            }

            if (appearance.IsSet(ForegroundProperty))
            {
                return appearance.Foreground;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves one property across the chain: the first appearance that explicitly SET it
    /// wins; <paramref name="fallback"/> otherwise. Unset detection (not value comparison)
    /// makes a page-level appearance a per-property delta.
    /// </summary>
    internal static T Resolve<T>(
        BindableProperty property,
        ScaffoldNavBarAppearance? page,
        ScaffoldNavBarAppearance? area,
        ScaffoldNavBarAppearance? scaffold,
        T fallback
    )
    {
        if (page is not null && page.IsSet(property))
        {
            return (T)page.GetValue(property);
        }

        if (area is not null && area.IsSet(property))
        {
            return (T)area.GetValue(property);
        }

        if (scaffold is not null && scaffold.IsSet(property))
        {
            return (T)scaffold.GetValue(property);
        }

        return fallback;
    }
}
