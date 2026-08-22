using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The default Nalu navigation bar component, created automatically as the
/// <see cref="Scaffold.NavBarTemplateProperty"/> default at the scaffold level. Slots, in order:
/// start-drawer button, back button, title (or the page's
/// <see cref="Scaffold.TitleViewProperty"/> content), end-drawer button, close button — all
/// driven by the <see cref="ScaffoldNavBarContext"/> binding context. Style it or replace it
/// entirely with any custom view.
/// </summary>
/// <remarks>
/// <para>
/// The component spans the whole top strip and opts into the safe area itself, so its content
/// sits below the status inset while the bar contributes its footprint to the page.
/// The strip BACKGROUND is not the bar's concern: it belongs to the chrome and is driven by the
/// effective the attached nav bar appearance properties (attached via
/// <c>Scaffold.NavBarBackground</c>) — the same is true for any custom bar.
/// </para>
/// <para>
/// It owns ONLY the strip metrics (height, padding, spacing). Title and button appearance
/// belong to the primitives — <see cref="ScaffoldNavBarTitle"/>, <see cref="ScaffoldBackButton"/>,
/// <see cref="ScaffoldCloseButton"/>, <see cref="ScaffoldFlyoutButton"/> — which are public and
/// styled directly, so the SAME style applies whether they sit in this bar or in a custom one:
/// <code>
/// &lt;Style TargetType="nalu:ScaffoldNavBarTitle"&gt;
///     &lt;Setter Property="FontFamily" Value="SemiBold" /&gt;
/// &lt;/Style&gt;
/// &lt;Style TargetType="nalu:ScaffoldNavBarButtonBase" ApplyToDerivedTypes="True"&gt;
///     &lt;Setter Property="IconColor" Value="{StaticResource Accent}" /&gt;
/// &lt;/Style&gt;
/// </code>
/// </para>
/// </remarks>
public sealed class ScaffoldNavBarView : Grid
{
    private readonly Grid _row;

    // Null-conditionals below: implicit styles apply from the VisualElement base ctor, before
    // _row exists; the ctor seeds the final values.

    /// <summary>Bindable property for <see cref="BarHeight"/>.</summary>
    public static readonly BindableProperty BarHeightProperty =
        GenericBindableProperty<ScaffoldNavBarView>.Create(
            nameof(BarHeight),
            48.0,
            propertyChanged: static view => (_, value) => view._row?.HeightRequest = value
        );

    /// <summary>Bindable property for <see cref="BarPadding"/>.</summary>
    public static readonly BindableProperty BarPaddingProperty =
        GenericBindableProperty<ScaffoldNavBarView>.Create(
            nameof(BarPadding),
            new Thickness(8, 0),
            propertyChanged: static view => (_, value) => view._row?.Padding = value
        );

    /// <summary>Bindable property for <see cref="Spacing"/>.</summary>
    public static readonly BindableProperty SpacingProperty =
        GenericBindableProperty<ScaffoldNavBarView>.Create(
            nameof(Spacing),
            8.0,
            propertyChanged: static view => (_, value) => view._row?.ColumnSpacing = value
        );

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
    /// (zero spacing): their 44dp tap targets' inner whitespace provides the optical rhythm
    /// around the 24dp glyphs.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Initializes the default nav bar.</summary>
    public ScaffoldNavBarView()
    {
        // A star-row root Grid FILLS bounded measure constraints — the single row must be Auto.
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // The bar spans the strip edge-to-edge and consumes the safe area itself: content sits
        // below the status inset (and clear of landscape notches), background covers it all.
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);

        // The leading buttons sit flush (zero spacing): the 44dp tap targets' inner whitespace
        // around the 24dp glyphs provides equal optical gaps — edge→glyph and glyph→glyph.
        // Hidden buttons are skipped entirely by the stack, so the rhythm survives every
        // visibility combination.
        var leadingButtons = new HorizontalStackLayout
                             {
                                 Spacing = 0,
                                 VerticalOptions = LayoutOptions.Center,
                                 Children =
                                 {
                                     new ScaffoldFlyoutButton { Side = ScaffoldFlyoutSide.Start, AutomationId = "NavBarFlyoutStartButton" },
                                     new ScaffoldBackButton { AutomationId = "NavBarBackButton" }
                                 }
                             };

        var trailingButtons = new HorizontalStackLayout
                              {
                                  Spacing = 0,
                                  Children =
                                  {
                                      new ScaffoldFlyoutButton { Side = ScaffoldFlyoutSide.End, AutomationId = "NavBarFlyoutEndButton" },
                                      new ScaffoldCloseButton { AutomationId = "NavBarCloseButton" }
                                  }
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
        _row.Add(new ScaffoldNavBarTitle { AutomationId = "NavBarTitle" }, 1);
        _row.Add(trailingButtons, 2);

        Add(_row);

        // Defaults never raise propertyChanged: seed once from the current values.
        _row.HeightRequest = BarHeight;
        _row.Padding = BarPadding;
        _row.ColumnSpacing = Spacing;
    }
}
