namespace Nalu;

/// <summary>
/// Base class for wrap layouts that arrange children in sequential positions, wrapping to the next row or column as necessary.
/// </summary>
public abstract class WrapLayout : Layout, IWrapLayout
{
    /// <summary>
    /// Bindable property for <see cref="ExpandMode"/>.
    /// </summary>
    public static readonly BindableProperty ExpandModeProperty = BindableProperty.Create(
        nameof(ExpandMode),
        typeof(WrapLayoutExpandMode),
        typeof(WrapLayout),
        WrapLayoutExpandMode.Distribute,
        propertyChanged: OnLayoutPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="HorizontalSpacing"/>.
    /// </summary>
    public static readonly BindableProperty HorizontalSpacingProperty = BindableProperty.Create(
        nameof(HorizontalSpacing),
        typeof(double),
        typeof(WrapLayout),
        0.0,
        propertyChanged: OnLayoutPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="VerticalSpacing"/>.
    /// </summary>
    public static readonly BindableProperty VerticalSpacingProperty = BindableProperty.Create(
        nameof(VerticalSpacing),
        typeof(double),
        typeof(WrapLayout),
        0.0,
        propertyChanged: OnLayoutPropertyChanged
    );

    /// <summary>
    /// Bindable property for <see cref="ItemsAlignment"/>.
    /// </summary>
    public static readonly BindableProperty ItemsAlignmentProperty = BindableProperty.Create(
        nameof(ItemsAlignment),
        typeof(WrapLayoutItemsAlignment),
        typeof(WrapLayout),
        WrapLayoutItemsAlignment.Start,
        propertyChanged: OnLayoutPropertyChanged
    );

    /// <summary>
    /// Attached bindable property for the expand ratio of a child view.
    /// </summary>
    public static readonly BindableProperty ExpandRatioProperty = BindableProperty.CreateAttached(
        "ExpandRatio",
        typeof(double),
        typeof(WrapLayout),
        0.0,
        propertyChanged: OnExpandRatioPropertyChanged
    );

    /// <summary>
    /// Gets the expand ratio for the specified view.
    /// </summary>
    /// <param name="view">The view to get the expand ratio from.</param>
    /// <returns>The expand ratio value.</returns>
    public static double GetExpandRatio(BindableObject view) => (double)view.GetValue(ExpandRatioProperty);

    /// <summary>
    /// Sets the expand ratio for the specified view.
    /// </summary>
    /// <param name="view">The view to set the expand ratio on.</param>
    /// <param name="value">The expand ratio value.</param>
    public static void SetExpandRatio(BindableObject view, double value) => view.SetValue(ExpandRatioProperty, value);

    /// <summary>
    /// Gets or sets the mode that defines how remaining space is distributed among items with an expand ratio greater than 0.
    /// </summary>
    public WrapLayoutExpandMode ExpandMode
    {
        get => (WrapLayoutExpandMode)GetValue(ExpandModeProperty);
        set => SetValue(ExpandModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal spacing between items.
    /// </summary>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical spacing between items.
    /// </summary>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the alignment of items within each line.
    /// </summary>
    /// <remarks>
    /// This is only effective when there's remaining space in the line after all items have been arranged.
    /// </remarks>
    public WrapLayoutItemsAlignment ItemsAlignment
    {
        get => (WrapLayoutItemsAlignment)GetValue(ItemsAlignmentProperty);
        set => SetValue(ItemsAlignmentProperty, value);
    }

    /// <inheritdoc />
    double IWrapLayout.GetExpandRatio(IView view) => GetExpandRatio((BindableObject)view);

    private static void OnLayoutPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is WrapLayout layout)
        {
            layout.InvalidateMeasure();
        }
    }

    private static void OnExpandRatioPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is IView { Parent: WrapLayout wrapLayout })
        {
            wrapLayout.InvalidateMeasure();
        }
    }
}
