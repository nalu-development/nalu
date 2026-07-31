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
/// working. <see cref="Foreground"/> is the one value flowing further: it reaches the
/// primitives through <see cref="ScaffoldNavBarContext.Foreground"/> as their color fallback.
/// </para>
/// <para>
/// The object is live: its properties are bindable (it inherits the binding context of the
/// element it is attached to) and mutations apply immediately — bind or animate
/// <see cref="Opacity"/>, <see cref="OffsetY"/> or a <see cref="SolidColorBrush.Color"/>
/// inside <see cref="Background"/> for scroll-driven chrome. An appearance instance declared
/// in a shared <see cref="Style"/> is one object attached to many elements: fine for constant
/// values, unsuitable for bindings.
/// </para>
/// </remarks>
public sealed class ScaffoldNavBarAppearance : BindableObject
{
    /// <summary>The default strip background used when no appearance in the chain sets one.</summary>
    internal static readonly Color DefaultBackgroundColor = Color.FromArgb("#F7FFFFFF");

    /// <summary>Bindable property for <see cref="Background"/>.</summary>
    public static readonly BindableProperty BackgroundProperty =
        BindableProperty.Create(nameof(Background), typeof(Brush), typeof(ScaffoldNavBarAppearance));

    /// <summary>Bindable property for <see cref="Foreground"/>.</summary>
    public static readonly BindableProperty ForegroundProperty =
        BindableProperty.Create(nameof(Foreground), typeof(Color), typeof(ScaffoldNavBarAppearance));

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
    /// A color set directly (or via style) on a primitive always wins over this.
    /// </summary>
    public Color? Foreground
    {
        get => (Color?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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
