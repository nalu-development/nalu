namespace Nalu;

/// <summary>
/// The default Nalu navigation bar component, created automatically as the
/// <see cref="Scaffold.NavBarViewProperty"/> default at the scaffold level. Slots, in order:
/// start-drawer button, back button, title (or the page's
/// <see cref="Scaffold.TitleViewProperty"/> content), end-drawer button — all driven by the
/// <see cref="ScaffoldNavBarContext"/> binding context. Style it (including with
/// <c>AppThemeBinding</c>) or replace it entirely with any custom view; the building blocks
/// (<see cref="ScaffoldBackButton"/>, <see cref="ScaffoldFlyoutButton"/>,
/// <see cref="ScaffoldNavBarTitle"/>) are public for custom bars.
/// </summary>
/// <remarks>
/// The component spans the whole top strip (its background extends under the status bar) and
/// opts into the safe area itself, so its content sits below the status inset while the bar
/// contributes its footprint to the page per §5.4.
/// </remarks>
public sealed class ScaffoldNavBarView : Grid
{
    private readonly Grid _row;
    private readonly ScaffoldFlyoutButton _flyoutStartButton;
    private readonly ScaffoldBackButton _backButton;
    private readonly ScaffoldNavBarTitle _title;
    private readonly ScaffoldFlyoutButton _flyoutEndButton;

    /// <summary>Bindable property for <see cref="BarBackground"/>.</summary>
    public static readonly BindableProperty BarBackgroundProperty =
        BindableProperty.Create(nameof(BarBackground), typeof(Brush), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="BarHeight"/>.</summary>
    public static readonly BindableProperty BarHeightProperty =
        BindableProperty.Create(nameof(BarHeight), typeof(double), typeof(ScaffoldNavBarView), 48.0, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="BarPadding"/>.</summary>
    public static readonly BindableProperty BarPaddingProperty =
        BindableProperty.Create(nameof(BarPadding), typeof(Thickness), typeof(ScaffoldNavBarView), Thickness.Zero, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="Spacing"/>.</summary>
    public static readonly BindableProperty SpacingProperty =
        BindableProperty.Create(nameof(Spacing), typeof(double), typeof(ScaffoldNavBarView), 8.0, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="IconColor"/>.</summary>
    public static readonly BindableProperty IconColorProperty =
        BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="TitleFontSize"/>.</summary>
    public static readonly BindableProperty TitleFontSizeProperty =
        BindableProperty.Create(nameof(TitleFontSize), typeof(double), typeof(ScaffoldNavBarView), 17.0, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="TitleFontAttributes"/>.</summary>
    public static readonly BindableProperty TitleFontAttributesProperty =
        BindableProperty.Create(nameof(TitleFontAttributes), typeof(FontAttributes), typeof(ScaffoldNavBarView), FontAttributes.Bold, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="BackIcon"/>.</summary>
    public static readonly BindableProperty BackIconProperty =
        BindableProperty.Create(nameof(BackIcon), typeof(ImageSource), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="FlyoutStartIcon"/>.</summary>
    public static readonly BindableProperty FlyoutStartIconProperty =
        BindableProperty.Create(nameof(FlyoutStartIcon), typeof(ImageSource), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Bindable property for <see cref="FlyoutEndIcon"/>.</summary>
    public static readonly BindableProperty FlyoutEndIconProperty =
        BindableProperty.Create(nameof(FlyoutEndIcon), typeof(ImageSource), typeof(ScaffoldNavBarView), null, propertyChanged: (b, _, _) => ((ScaffoldNavBarView)b).ApplyStyling());

    /// <summary>Gets or sets the bar background (extends under the status bar). Theme-aware translucent surface when unset.</summary>
    public Brush? BarBackground
    {
        get => (Brush?)GetValue(BarBackgroundProperty);
        set => SetValue(BarBackgroundProperty, value);
    }

    /// <summary>Gets or sets the bar content height (excluding the status-bar inset).</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>Gets or sets the padding around the bar content.</summary>
    public Thickness BarPadding
    {
        get => (Thickness)GetValue(BarPaddingProperty);
        set => SetValue(BarPaddingProperty, value);
    }

    /// <summary>
    /// Gets or sets the gap around the title column. The icon buttons themselves sit flush
    /// (zero spacing, zero leading padding by default): their 44dp tap targets' inner
    /// whitespace provides the optical rhythm around the 24dp glyphs.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Gets or sets the title color. Theme-aware default when unset.</summary>
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Gets or sets the glyph color of the back/drawer buttons. Theme-aware default when unset.</summary>
    public Color? IconColor
    {
        get => (Color?)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    /// <summary>Gets or sets the title font family.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Gets or sets the title font size.</summary>
    public double TitleFontSize
    {
        get => (double)GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    /// <summary>Gets or sets the title font attributes.</summary>
    public FontAttributes TitleFontAttributes
    {
        get => (FontAttributes)GetValue(TitleFontAttributesProperty);
        set => SetValue(TitleFontAttributesProperty, value);
    }

    /// <summary>Gets or sets an icon replacing the built-in back chevron.</summary>
    public ImageSource? BackIcon
    {
        get => (ImageSource?)GetValue(BackIconProperty);
        set => SetValue(BackIconProperty, value);
    }

    /// <summary>Gets or sets an icon replacing the built-in start-drawer hamburger glyph.</summary>
    public ImageSource? FlyoutStartIcon
    {
        get => (ImageSource?)GetValue(FlyoutStartIconProperty);
        set => SetValue(FlyoutStartIconProperty, value);
    }

    /// <summary>Gets or sets an icon replacing the built-in end-drawer hamburger glyph.</summary>
    public ImageSource? FlyoutEndIcon
    {
        get => (ImageSource?)GetValue(FlyoutEndIconProperty);
        set => SetValue(FlyoutEndIconProperty, value);
    }

    /// <summary>Initializes the default nav bar.</summary>
    public ScaffoldNavBarView()
    {
        // A star-row root Grid FILLS bounded measure constraints — the single row must be Auto.
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // The bar spans the strip edge-to-edge and consumes the safe area itself: content sits
        // below the status inset (and clear of landscape notches), background covers it all.
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);

        _flyoutStartButton = new ScaffoldFlyoutButton { Side = ScaffoldFlyoutSide.Start, AutomationId = "NavBarFlyoutStartButton" };
        _backButton = new ScaffoldBackButton { AutomationId = "NavBarBackButton" };
        _title = new ScaffoldNavBarTitle { AutomationId = "NavBarTitle" };
        _flyoutEndButton = new ScaffoldFlyoutButton { Side = ScaffoldFlyoutSide.End, AutomationId = "NavBarFlyoutEndButton" };

        // The leading buttons sit flush (zero spacing): the 44dp tap targets' inner whitespace
        // around the 24dp glyphs provides equal optical gaps — edge→glyph and glyph→glyph.
        // Hidden buttons are skipped entirely by the stack, so the rhythm survives every
        // visibility combination.
        var leadingButtons = new HorizontalStackLayout
                             {
                                 Spacing = 0,
                                 VerticalOptions = LayoutOptions.Center,
                                 Children = { _flyoutStartButton, _backButton }
                             };

        _row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        _row.Add(leadingButtons, 0);
        _row.Add(_title, 1);
        _row.Add(_flyoutEndButton, 2);

        Add(_row);

        if (Application.Current is { } application)
        {
            application.RequestedThemeChanged += (_, _) => ApplyStyling();
        }

        ApplyStyling();
    }

    private void ApplyStyling()
    {
        var dark = ScaffoldNavBarDefaults.IsDark;

        Background = BarBackground ?? new SolidColorBrush(ScaffoldNavBarDefaults.BarBackground(dark));

        _row.HeightRequest = BarHeight;
        _row.Padding = BarPadding;
        _row.ColumnSpacing = Spacing;

        _backButton.IconColor = IconColor;
        _backButton.Icon = BackIcon;
        _flyoutStartButton.IconColor = IconColor;
        _flyoutStartButton.Icon = FlyoutStartIcon;
        _flyoutEndButton.IconColor = IconColor;
        _flyoutEndButton.Icon = FlyoutEndIcon;

        _title.TextColor = TextColor;
        _title.FontFamily = FontFamily;
        _title.FontSize = TitleFontSize;
        _title.FontAttributes = TitleFontAttributes;
    }
}
