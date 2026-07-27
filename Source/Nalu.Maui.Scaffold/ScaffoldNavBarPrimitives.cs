using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using ShapePath = Microsoft.Maui.Controls.Shapes.Path;

namespace Nalu;

/// <summary>Theme-aware defaults shared by the nav bar component and its primitives.</summary>
internal static class ScaffoldNavBarDefaults
{
    internal static bool IsDark => (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;

    internal static Color Foreground(bool dark) => dark ? Colors.White : Color.FromArgb("#1C1C1E");

    internal static Color BarBackground(bool dark) => dark ? Color.FromArgb("#F72E2E2E") : Color.FromArgb("#F7FFFFFF");

    internal static Geometry ParseGeometry(string pathData)
        => (Geometry)new PathGeometryConverter().ConvertFromInvariantString(pathData)!;
}

/// <summary>
/// Base of the nav bar glyph buttons: a fixed 44dp tap target hosting a Nalu-drawn stroke glyph
/// (or a user icon), visibility and command bound to the inherited
/// <see cref="ScaffoldNavBarContext"/>.
/// </summary>
public abstract class ScaffoldNavBarButtonBase : Border
{
    private readonly ShapePath _glyph;
    private readonly Image _iconImage;
    private readonly TapGestureRecognizer _tap = new();

    /// <summary>Bindable property for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(ImageSource), typeof(ScaffoldNavBarButtonBase), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarButtonBase)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="IconColor"/>.</summary>
    public static readonly BindableProperty IconColorProperty =
        BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(ScaffoldNavBarButtonBase), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarButtonBase)b).ApplyStyling());

    /// <summary>Gets or sets an icon replacing the built-in drawn glyph (rendered untinted).</summary>
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets the built-in glyph color. Theme-aware default when unset.</summary>
    public Color? IconColor
    {
        get => (Color?)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
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

        Content = new Grid { Children = { _glyph, _iconImage } };

        GestureRecognizers.Add(_tap);

        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged += (_, _) => ApplyStyling();
        }

        ApplyStyling();
    }

    /// <summary>Binds the tap command with the given (trim-safe, typed) binding.</summary>
    private protected void BindCommand(BindingBase binding) => _tap.SetBinding(TapGestureRecognizer.CommandProperty, binding);

    /// <summary>Binds visibility with the given (trim-safe, typed) binding.</summary>
    private protected void BindVisibility(BindingBase binding) => this.SetBinding(IsVisibleProperty, binding);

    private protected void ApplyStyling()
    {
        var userIcon = Icon;
        _iconImage.IsVisible = userIcon is not null;
        _iconImage.Source = userIcon;
        _glyph.IsVisible = userIcon is null;
        _glyph.Stroke = new SolidColorBrush(IconColor ?? ScaffoldNavBarDefaults.Foreground(ScaffoldNavBarDefaults.IsDark));
    }
}

/// <summary>
/// The nav bar back button: visible while the current stack has pushed pages, pops through the
/// navigation engine (guards run). Drop it anywhere inside a custom nav bar — it binds to the
/// inherited <see cref="ScaffoldNavBarContext"/>.
/// </summary>
public sealed class ScaffoldBackButton : ScaffoldNavBarButtonBase
{
    /// <summary>Initializes the back button (built-in chevron glyph).</summary>
    public ScaffoldBackButton()
        : base("M14.5 5.5 L7.5 12 L14.5 18.5")
    {
        // Typed bindings: string paths resolve via reflection and break under trimming/AOT.
        BindVisibility(Binding.Create(static (ScaffoldNavBarContext c) => c.CanNavigateBack));
        BindCommand(Binding.Create(static (ScaffoldNavBarContext c) => c.BackCommand));
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
        BindVisibility(Binding.Create(static (ScaffoldNavBarContext c) => c.IsCloseButtonVisible));
        BindCommand(Binding.Create(static (ScaffoldNavBarContext c) => c.BackCommand));
    }
}

/// <summary>
/// A nav bar drawer button: opens the corresponding flyout; visible when its content resolves
/// and its <see cref="ScaffoldFlyoutButtonVisibility"/> policy allows it. Drop it anywhere
/// inside a custom nav bar — it binds to the inherited <see cref="ScaffoldNavBarContext"/>.
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
            BindVisibility(Binding.Create(static (ScaffoldNavBarContext c) => c.IsFlyoutStartButtonVisible));
            BindCommand(Binding.Create(static (ScaffoldNavBarContext c) => c.OpenFlyoutStartCommand));
        }
        else
        {
            BindVisibility(Binding.Create(static (ScaffoldNavBarContext c) => c.IsFlyoutEndButtonVisible));
            BindCommand(Binding.Create(static (ScaffoldNavBarContext c) => c.OpenFlyoutEndCommand));
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

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ScaffoldNavBarTitle), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarTitle)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(ScaffoldNavBarTitle), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarTitle)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(ScaffoldNavBarTitle), 17.0, propertyChanged: (b, _, _) => ((ScaffoldNavBarTitle)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty =
        BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(ScaffoldNavBarTitle), FontAttributes.Bold, propertyChanged: (b, _, _) => ((ScaffoldNavBarTitle)b).ApplyStyling());

    /// <summary>Gets or sets the title color. Theme-aware default when unset.</summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
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
        _label.SetBinding(Label.TextProperty, Binding.Create(static (ScaffoldNavBarContext c) => c.Title));
        Add(_label);

        VerticalOptions = LayoutOptions.Center;

        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged += (_, _) => ApplyStyling();
        }

        ApplyStyling();
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

        UpdateTitleView();
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaffoldNavBarContext.TitleView))
        {
            UpdateTitleView();
        }
    }

    private void UpdateTitleView()
    {
        var titleView = _observedContext?.TitleView;

        // Remove any previously hosted title view (children: label + optional title view).
        for (var i = Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(this[i], _label))
            {
                RemoveAt(i);
            }
        }

        if (titleView is not null)
        {
            _label.IsVisible = false;
            Add(titleView);
        }
        else
        {
            _label.IsVisible = true;
        }
    }

    private void ApplyStyling()
    {
        _label.TextColor = TextColor ?? ScaffoldNavBarDefaults.Foreground(ScaffoldNavBarDefaults.IsDark);
        _label.FontFamily = FontFamily;
        _label.FontSize = FontSize;
        _label.FontAttributes = FontAttributes;
    }
}
