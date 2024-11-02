namespace Nalu;

/// <summary>
/// ContentLayout is a layout that is used to display a single view.
/// </summary>
/// <remarks>
/// Can be used as a replacement of <see cref="ContentView"/> (which as-of .NET 8 uses Compatibility.Layout).
/// </remarks>
[ContentProperty(nameof(Content))]
public partial class ViewBox : ViewBoxBase
{
    /// <summary>
    /// Bindable property for <see cref="Content"/> property.
    /// </summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(IView), typeof(ViewBox), propertyChanged: OnContentPropertyChanged);

    /// <summary>
    /// Gets or sets the content of the layout.
    /// </summary>
    public IView? Content
    {
        get => GetContent();
        set => SetContent(value);
    }

    /// <inheritdoc />
    protected override IView? GetContent() => (IView?)GetValue(ContentProperty);

    /// <inheritdoc />
    protected override void SetContent(IView? content) => SetValue(ContentProperty, content);

    private static void OnContentPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((ViewBox)bindable).OnContentPropertyChanged((IView?)oldValue, (IView?)newValue);
}
