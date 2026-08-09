using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using Nalu.Internals;
using ShapePath = Microsoft.Maui.Controls.Shapes.Path;

namespace Nalu;

/// <summary>Shared helpers of the nav bar component and its primitives.</summary>
internal static class ScaffoldNavBarDefaults
{
    /// <summary>The default foreground of every nav bar primitive (title text, drawn glyphs).</summary>
    // ReSharper disable once InconsistentNaming
    internal static readonly Color Foreground = Color.FromArgb("#1C1C1E");

    internal static Geometry ParseGeometry(string pathData)
        => (Geometry)new PathGeometryConverter().ConvertFromInvariantString(pathData)!;
}

/// <summary>
/// Base of the nav bar glyph buttons: a fixed 44dp tap target hosting a Nalu-drawn stroke glyph
/// (or a user icon), visibility and command bound to the inherited
/// <see cref="ScaffoldNavBarContext"/>.
/// </summary>
/// <remarks>
/// Style the concrete buttons directly, or all of them at once with
/// <c>&lt;Style TargetType="nalu:ScaffoldNavBarButtonBase" ApplyToDerivedTypes="True"&gt;</c>.
/// </remarks>
public abstract class ScaffoldNavBarButtonBase : Border
{
    private readonly ShapePath _glyph;
    private readonly Image _iconImage;
    private readonly Ellipse _pressHighlight;
    private readonly TapGestureRecognizer _tap = new();
    private ScaffoldNavBarContext? _observedContext;

    // Callback caveat (applies to EVERY styling property here): implicit styles are applied by
    // the VisualElement BASE ctor (MergedStyle), before this class's ctor body has built its
    // subviews — callbacks must tolerate null fields; the ctor seeds the final values.

    /// <summary>Bindable property for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty =
        GenericBindableProperty<ScaffoldNavBarButtonBase>.Create<ImageSource?>(
            nameof(Icon),
            propertyChanged: static button => (_, value) => button.ApplyIcon(value)
        );

    /// <summary>Bindable property for <see cref="IconColor"/>.</summary>
    public static readonly BindableProperty IconColorProperty =
        GenericBindableProperty<ScaffoldNavBarButtonBase>.Create(
            nameof(IconColor),
            ScaffoldNavBarDefaults.Foreground,
            propertyChanged: static button => (_, _) => button.ApplyEffectiveColors()
        );

    /// <summary>Bindable property for <see cref="PressedBrush"/>.</summary>
    public static readonly BindableProperty PressedBrushProperty =
        GenericBindableProperty<ScaffoldNavBarButtonBase>.Create<Brush?>(
            nameof(PressedBrush),
            propertyChanged: static button => (_, _) => button.ApplyEffectiveColors()
        );

    /// <summary>Gets or sets an icon replacing the built-in drawn glyph (rendered untinted).</summary>
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the built-in glyph color (ignored while <see cref="Icon"/> is set).
    /// When not set (directly or via style), the effective
    /// <see cref="ScaffoldNavBarContext.Foreground"/> applies.
    /// </summary>
    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the press-feedback brush (a circular pulse filling the 44dp tap target).
    /// When not set, a translucent tint of the effective glyph color is used.
    /// </summary>
    public Brush? PressedBrush
    {
        get => (Brush?)GetValue(PressedBrushProperty);
        set => SetValue(PressedBrushProperty, value);
    }

    private protected ScaffoldNavBarButtonBase(string glyphPathData)
    {
        StrokeThickness = 0;
        Background = null;
        WidthRequest = 44;
        HeightRequest = 44;
        VerticalOptions = LayoutOptions.Center;

        // The glyph geometries are designed centered in a 24-box: the explicit 24x24 size makes
        // the drawn glyph EXACTLY centered in the 44dp tap target (without it, the shape sizes
        // to the geometry's own bounds and the optical center drifts).
        _glyph = new ShapePath
        {
            Data = ScaffoldNavBarDefaults.ParseGeometry(glyphPathData),
            StrokeThickness = 2.2,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        _iconImage = new Image
        {
            Aspect = Aspect.AspectFit,
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        };

        // The pulse sits BELOW the glyph, filling the tap target; InputTransparent keeps it out
        // of every hit-test path.
        _pressHighlight = new Ellipse
        {
            WidthRequest = 44,
            HeightRequest = 44,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0,
            InputTransparent = true
        };

        var touchSurface = new Grid { Children = { _pressHighlight, _glyph, _iconImage } };
        Content = touchSurface;

        GestureRecognizers.Add(_tap);
        ScaffoldPressable.Observe(touchSurface, OnPressedPulse);

        // Defaults never raise propertyChanged: seed once from the current values.
        ApplyEffectiveColors();
        ApplyIcon(Icon);
    }

    /// <summary>Binds the tap command with the given (trim-safe, typed) binding.</summary>
    private protected void BindCommand(BindingBase binding) => _tap.SetBinding(TapGestureRecognizer.CommandProperty, binding);

    /// <summary>Binds visibility with the given (trim-safe, typed) binding.</summary>
    private protected void BindVisibility(BindingBase binding) => this.SetBinding(IsVisibleProperty, binding);

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_observedContext is not null)
        {
            _observedContext.PropertyChanged -= OnContextPropertyChanged;
            _observedContext = null;
        }

        if (BindingContext is ScaffoldNavBarContext context)
        {
            _observedContext = context;
            context.PropertyChanged += OnContextPropertyChanged;
        }

        ApplyEffectiveColors();
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaffoldNavBarContext.Foreground))
        {
            ApplyEffectiveColors();
        }
    }

    /// <summary>
    /// The effective glyph color: an explicitly set (or styled) <see cref="IconColor"/> wins,
    /// then the appearance-driven context foreground, then the built-in default. Read-path
    /// only — nothing ever writes into <see cref="IconColorProperty"/>, so styles keep working.
    /// </summary>
    private void ApplyEffectiveColors()
    {
        if (_glyph is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the subviews.
            return;
        }

        var color = IsSet(IconColorProperty)
            ? IconColor
            : _observedContext?.Foreground ?? ScaffoldNavBarDefaults.Foreground;

        _glyph.Stroke = new SolidColorBrush(color);
        _pressHighlight.Fill = PressedBrush ?? new SolidColorBrush(color.WithAlpha(0.14f));
    }

    /// <summary>The press feedback: an instant-on, self-fading pulse (see <see cref="ScaffoldPressable"/>).</summary>
    private void OnPressedPulse()
    {
        Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(_pressHighlight);
        _pressHighlight.Opacity = 1;
        _ = _pressHighlight.FadeToAsync(0, 400, Easing.CubicOut);
    }

    /// <summary>A user icon replaces the drawn glyph entirely (and renders untinted).</summary>
    private void ApplyIcon(ImageSource? icon)
    {
        if (_iconImage is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the subviews.
            return;
        }

        _iconImage.Source = icon;
        _iconImage.IsVisible = icon is not null;
        _glyph.IsVisible = icon is null;
    }
}

/// <summary>
/// The nav bar back button: visible while the stack has pushed pages and the current page is
/// not modal; pops through the navigation engine (guards run). Drop it anywhere inside a
/// custom nav bar — it binds to the inherited <see cref="ScaffoldNavBarContext"/>.
/// </summary>
public sealed class ScaffoldBackButton : ScaffoldNavBarButtonBase
{
    /// <summary>Initializes the back button (built-in chevron glyph).</summary>
    public ScaffoldBackButton()
        : base("M14.5 5.5 L7.5 12 L14.5 18.5")
    {
        // Typed bindings: string paths resolve via reflection and break under trimming/AOT.
        BindVisibility(BindingBase.Create(static (ScaffoldNavBarContext c) => c.CanNavigateBack));
        BindCommand(BindingBase.Create(static (ScaffoldNavBarContext c) => c.BackCommand));
    }
}

/// <summary>
/// A nav bar close (X) button for modal pages: pops through the navigation engine (guards and
/// lifecycle run); visible on <see cref="ScaffoldPageMode.DismissableModal"/> pages. Drop it
/// anywhere inside a custom nav bar — it binds to the inherited <see cref="ScaffoldNavBarContext"/>.
/// </summary>
public sealed class ScaffoldCloseButton : ScaffoldNavBarButtonBase
{
    /// <summary>Initializes the close button (built-in X glyph).</summary>
    public ScaffoldCloseButton()
        : base("M6.5 6.5 L17.5 17.5 M17.5 6.5 L6.5 17.5")
    {
        BindVisibility(BindingBase.Create(static (ScaffoldNavBarContext c) => c.IsCloseButtonVisible));
        BindCommand(BindingBase.Create(static (ScaffoldNavBarContext c) => c.BackCommand));
    }
}

/// <summary>
/// A nav bar drawer button: opens the corresponding flyout; visible when its content resolves,
/// the page is not modal, and its <see cref="ScaffoldFlyoutButtonVisibility"/> policy allows
/// it (by default only at the stack root). Drop it anywhere inside a custom nav bar — it binds
/// to the inherited <see cref="ScaffoldNavBarContext"/>.
/// </summary>
public sealed class ScaffoldFlyoutButton : ScaffoldNavBarButtonBase
{
    /// <summary>Bindable property for <see cref="Side"/>.</summary>
    public static readonly BindableProperty SideProperty =
        BindableProperty.Create(nameof(Side), typeof(ScaffoldFlyoutSide), typeof(ScaffoldFlyoutButton), ScaffoldFlyoutSide.Start, propertyChanged: (b, _, _) => ((ScaffoldFlyoutButton)b).Rebind());

    /// <summary>Gets or sets the edge this button opens.</summary>
    public ScaffoldFlyoutSide Side
    {
        get => (ScaffoldFlyoutSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>Initializes the drawer button (built-in hamburger glyph).</summary>
    public ScaffoldFlyoutButton()
        : base("M5 7.5 L19 7.5 M5 12 L19 12 M5 16.5 L19 16.5")
    {
        Rebind();
    }

    private void Rebind()
    {
        if (Side == ScaffoldFlyoutSide.Start)
        {
            BindVisibility(BindingBase.Create(static (ScaffoldNavBarContext c) => c.IsFlyoutStartButtonVisible));
            BindCommand(BindingBase.Create(static (ScaffoldNavBarContext c) => c.OpenFlyoutStartCommand));
        }
        else
        {
            BindVisibility(BindingBase.Create(static (ScaffoldNavBarContext c) => c.IsFlyoutEndButtonVisible));
            BindCommand(BindingBase.Create(static (ScaffoldNavBarContext c) => c.OpenFlyoutEndCommand));
        }
    }
}

/// <summary>
/// The nav bar title: renders the current page's <see cref="Scaffold.TitleViewProperty"/>
/// content when set, the page <see cref="Page.Title"/> otherwise. Drop it anywhere inside a
/// custom nav bar — it binds to the inherited <see cref="ScaffoldNavBarContext"/>.
/// </summary>
public sealed class ScaffoldNavBarTitle : Grid
{
    private readonly Label _label;
    private ScaffoldNavBarContext? _observedContext;

    // Null-conditionals below: implicit styles apply from the VisualElement base ctor, before
    // _label exists; the ctor seeds the final values.

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        GenericBindableProperty<ScaffoldNavBarTitle>.Create(
            nameof(TextColor),
            ScaffoldNavBarDefaults.Foreground,
            propertyChanged: static title => (_, _) => title.ApplyEffectiveTextColor()
        );

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        GenericBindableProperty<ScaffoldNavBarTitle>.Create<string?>(
            nameof(FontFamily),
            propertyChanged: static title => (_, value) => title._label?.FontFamily = value
        );

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        GenericBindableProperty<ScaffoldNavBarTitle>.Create(
            nameof(FontSize),
            17.0,
            propertyChanged: static title => (_, value) => title._label?.FontSize = value
        );

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty =
        GenericBindableProperty<ScaffoldNavBarTitle>.Create(
            nameof(FontAttributes),
            FontAttributes.Bold,
            propertyChanged: static title => (_, value) => title._label?.FontAttributes = value
        );

    /// <summary>
    /// Gets or sets the title color. When not set (directly or via style), the effective
    /// <see cref="ScaffoldNavBarContext.Foreground"/> applies, then the built-in default.
    /// </summary>
    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the title font family.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the title font size.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Gets or sets the title font attributes.</summary>
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>Initializes the title presenter.</summary>
    public ScaffoldNavBarTitle()
    {
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        _label = new Label
        {
            AutomationId = "NavBarTitleLabel",
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        _label.SetBinding(Label.TextProperty, static (ScaffoldNavBarContext c) => c.Title);
        Add(_label);

        VerticalOptions = LayoutOptions.Center;

        // Defaults never raise propertyChanged: seed once from the current values.
        ApplyEffectiveTextColor();
        _label.FontFamily = FontFamily;
        _label.FontSize = FontSize;
        _label.FontAttributes = FontAttributes;
    }

    /// <summary>
    /// The effective title color: an explicitly set (or styled) <see cref="TextColor"/> wins,
    /// then the appearance-driven context foreground, then the built-in default. Read-path
    /// only — nothing ever writes into <see cref="TextColorProperty"/>, so styles keep working.
    /// </summary>
    private void ApplyEffectiveTextColor()
    {
        if (_label is null)
        {
            // Style applied from the base ctor — the ctor seeds after building the subviews.
            return;
        }

        _label.TextColor = IsSet(TextColorProperty)
            ? TextColor
            : _observedContext?.Foreground ?? ScaffoldNavBarDefaults.Foreground;
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_observedContext is not null)
        {
            _observedContext.PropertyChanged -= OnContextPropertyChanged;
            _observedContext = null;
        }

        if (BindingContext is ScaffoldNavBarContext context)
        {
            _observedContext = context;
            context.PropertyChanged += OnContextPropertyChanged;
        }

        ApplyEffectiveTextColor();
        UpdateTitleView();
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ScaffoldNavBarContext.TitleView):
                UpdateTitleView();

                break;

            case nameof(ScaffoldNavBarContext.PageBindingContext):
                ApplyTitleViewBindingContext();

                break;

            case nameof(ScaffoldNavBarContext.Foreground):
                ApplyEffectiveTextColor();

                break;
        }
    }

    private void UpdateTitleView()
    {
        var titleView = _observedContext?.TitleView;

        // Remove any previously hosted title view (children: label + optional title view).
        for (var i = Count - 1; i >= 0; i--)
        {
            var child = this[i];
            if (!ReferenceEquals(child, _label))
            {
                RemoveAt(i);
                (child as BindableObject)?.BindingContext = null;
            }
        }

        if (titleView is not null)
        {
            _label.IsVisible = false;
            ApplyTitleViewBindingContext();
            Add(titleView);
        }
        else
        {
            _label.IsVisible = true;
        }
    }

    /// <summary>
    /// TitleView content is PAGE content: it binds the current page's model, not this bar's
    /// context. Adding it above propagated the slot's own context as inherited value — this
    /// overrides it with the page's (a user-assigned BindingContext always wins over both).
    /// </summary>
    private void ApplyTitleViewBindingContext()
    {
        if (_observedContext is { TitleView: { } titleView } context)
        {
            titleView.BindingContext = context.PageBindingContext;
        }
    }
}
