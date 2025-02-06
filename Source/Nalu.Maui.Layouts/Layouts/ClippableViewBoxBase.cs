namespace Nalu;

/// <summary>
/// Extends <see cref="ViewBoxBase"/> base class with customizable clipping behavior.
/// </summary>
public class ClippableViewBoxBase : ViewBoxBase, IViewBox
{
    /// <summary>
    /// Bindable property for <see cref="IsClippedToBounds"/>.
    /// </summary>
    public static readonly BindableProperty IsClippedToBoundsProperty =
        BindableProperty.Create(nameof(IsClippedToBounds), typeof(bool), typeof(Layout), false, propertyChanged: IsClippedToBoundsPropertyChanged);

    /// <summary>
    /// Gets or sets a value which determines if the layout should clip its children to its bounds.
    /// The default value is <see langword="false"/>.
    /// </summary>
    public bool IsClippedToBounds
    {
        get => (bool)GetValue(IsClippedToBoundsProperty);
        set => SetValue(IsClippedToBoundsProperty, value);
    }

    bool IViewBox.ClipsToBounds => IsClippedToBounds;

    private static void IsClippedToBoundsPropertyChanged(BindableObject bindableObject, object oldValue, object newValue)
    {
        if (bindableObject is IView view)
        {
            view.Handler?.UpdateValue(nameof(IViewBox.ClipsToBounds));
        }
    }
}
