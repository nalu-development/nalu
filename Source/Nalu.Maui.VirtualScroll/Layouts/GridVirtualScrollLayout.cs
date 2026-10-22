namespace Nalu;

/// <summary>
/// A grid layout for virtual scroll that arranges items in lines of <see cref="Span" /> cells.
/// </summary>
/// <remarks>
/// <para>
/// A line is a row when the layout scrolls vertically and a column when it scrolls horizontally.
/// Cells share the line extent along the scrolling axis: the line is as long as its longest cell
/// and every cell in it is stretched to match, so cell backgrounds and borders align.
/// </para>
/// <para>
/// The global header and footer, and every section header and footer, always take a whole line,
/// and a section never shares a line with another section — a section always starts on a new line
/// and its trailing partial line is left unfilled.
/// </para>
/// <para>
/// The properties below are read when the layout is applied to the control. To change the grid at
/// runtime — a different span on rotation, for instance — assign a new layout instance to
/// <see cref="VirtualScroll.ItemsLayout" />.
/// </para>
/// </remarks>
public abstract class GridVirtualScrollLayout : LinearVirtualScrollLayout
{
    /// <summary>
    /// Bindable property for <see cref="Span" />.
    /// </summary>
    public static readonly BindableProperty SpanProperty =
        BindableProperty.Create(
            nameof(Span),
            typeof(int),
            typeof(GridVirtualScrollLayout),
            2,
            BindingMode.OneTime,
            validateValue: static (_, value) => value is >= 1
        );

    /// <summary>
    /// Gets or sets the number of cells per line — columns when the layout scrolls vertically,
    /// rows when it scrolls horizontally. Defaults to 2, and must be at least 1.
    /// </summary>
    public int Span
    {
        get => (int) GetValue(SpanProperty);
        set => SetValue(SpanProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="ItemSpacing" />.
    /// </summary>
    public static readonly BindableProperty ItemSpacingProperty =
        BindableProperty.Create(
            nameof(ItemSpacing),
            typeof(double),
            typeof(GridVirtualScrollLayout),
            0d,
            BindingMode.OneTime,
            validateValue: static (_, value) => value is double and >= 0 and < double.PositiveInfinity
        );

    /// <summary>
    /// Gets or sets the gap between cells within the same line, in device-independent units.
    /// </summary>
    /// <remarks>
    /// The gap is taken out of the space the cells share: each cell gets
    /// <c>(available - (Span - 1) * ItemSpacing) / Span</c> along the cross axis.
    /// </remarks>
    public double ItemSpacing
    {
        get => (double) GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    /// <summary>
    /// Bindable property for <see cref="LineSpacing" />.
    /// </summary>
    public static readonly BindableProperty LineSpacingProperty =
        BindableProperty.Create(
            nameof(LineSpacing),
            typeof(double),
            typeof(GridVirtualScrollLayout),
            0d,
            BindingMode.OneTime,
            validateValue: static (_, value) => value is double and >= 0 and < double.PositiveInfinity
        );

    /// <summary>
    /// Gets or sets the gap between consecutive lines along the scrolling axis, in
    /// device-independent units.
    /// </summary>
    /// <remarks>
    /// Headers and footers are lines too, so the gap also separates them from the items next to them.
    /// </remarks>
    public double LineSpacing
    {
        get => (double) GetValue(LineSpacingProperty);
        set => SetValue(LineSpacingProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GridVirtualScrollLayout" /> class.
    /// </summary>
    /// <param name="orientation">The orientation of the layout.</param>
    protected GridVirtualScrollLayout(ItemsLayoutOrientation orientation)
        : base(orientation)
    {
    }
}
