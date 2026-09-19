using Nalu.Internals;

namespace Nalu;

/// <summary>
/// The default Nalu navigation bar component, created automatically as the
/// <see cref="Scaffold.NavBarTemplateProperty"/> default at the scaffold level. Slots, in order:
/// start-drawer button, back button, title (or the page's
/// <see cref="Scaffold.TitleViewProperty"/> content), the page's <see cref="Page.ToolbarItems"/>
/// (as many primary items as fit, the rest and the secondary ones behind an overflow menu — see
/// <see cref="ScaffoldToolbarItemsView"/>; the title is <see cref="KeepTitleWhole"/> above
/// <see cref="MinimumTitleWidth"/>),
/// end-drawer button, close button — all driven by the
/// <see cref="ScaffoldNavBarContext"/> binding context. Style it or replace it entirely with
/// any custom view.
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
/// It owns ONLY the strip metrics (height, padding, spacing, the title's reservation). Title and button appearance
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
    private readonly ScaffoldNavBarRow _row;

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
            propertyChanged: static view => (_, value) => view._row?.Spacing = value
        );

    /// <summary>Bindable property for <see cref="KeepTitleWhole"/>.</summary>
    public static readonly BindableProperty KeepTitleWholeProperty =
        GenericBindableProperty<ScaffoldNavBarView>.Create(
            nameof(KeepTitleWhole),
            true,
            propertyChanged: static view => (_, value) => view._row?.KeepTitleWhole = value
        );

    /// <summary>Bindable property for <see cref="MinimumTitleWidth"/>.</summary>
    public static readonly BindableProperty MinimumTitleWidthProperty =
        GenericBindableProperty<ScaffoldNavBarView>.Create(
            nameof(MinimumTitleWidth),
            96.0,
            propertyChanged: static view => (_, value) => view._row?.MinimumTitleWidth = value
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

    /// <summary>
    /// Gets or sets whether the title (or title view) reserves its natural width before the
    /// page's toolbar items are offered the rest of the row (true by default): items fold into
    /// the overflow menu, the title is never truncated by them. False reduces the reservation to
    /// <see cref="MinimumTitleWidth"/> — the native trade-off, where items win and the title
    /// truncates. Native bars let a long item list eat the title; this bar doesn't.
    /// </summary>
    public bool KeepTitleWhole
    {
        get => (bool)GetValue(KeepTitleWholeProperty);
        set => SetValue(KeepTitleWholeProperty, value);
    }

    /// <summary>
    /// Gets or sets the width the title keeps whatever the page's toolbar items ask for (96 by
    /// default) — the floor of the reservation (see <see cref="KeepTitleWhole"/>): the items
    /// strip is offered the rest of the row and folds what doesn't fit into its overflow menu.
    /// Lower it (a style, or a page-level template) for a bar that favors items.
    /// </summary>
    public double MinimumTitleWidth
    {
        get => (double)GetValue(MinimumTitleWidthProperty);
        set => SetValue(MinimumTitleWidthProperty, value);
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

        // Toolbar items come first on the trailing side: the drawer and close buttons keep the
        // outer edge, where a thumb expects them, whatever the page contributes. The strip is
        // its own row slot, measured with what the fixed parts and the title's floor leave.
        var trailingButtons = new HorizontalStackLayout
                              {
                                  Spacing = 0,
                                  VerticalOptions = LayoutOptions.Center,
                                  Children =
                                  {
                                      new ScaffoldFlyoutButton { Side = ScaffoldFlyoutSide.End, AutomationId = "NavBarFlyoutEndButton" },
                                      new ScaffoldCloseButton { AutomationId = "NavBarCloseButton" }
                                  }
                              };

        _row = new ScaffoldNavBarRow(
            leadingButtons,
            new ScaffoldNavBarTitle { AutomationId = "NavBarTitle" },
            new ScaffoldToolbarItemsView { AutomationId = "NavBarToolbarItems" },
            trailingButtons
        );

        Add(_row);

        // Defaults never raise propertyChanged: seed once from the current values.
        _row.HeightRequest = BarHeight;
        _row.Padding = BarPadding;
        _row.Spacing = Spacing;
        _row.MinimumTitleWidth = MinimumTitleWidth;
        _row.KeepTitleWhole = KeepTitleWhole;
    }
}
