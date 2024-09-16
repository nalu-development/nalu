namespace Nalu;

/// <summary>
/// ContentLayout is a layout that is used to display a single view.
/// </summary>
/// <remarks>
/// Can be used as a replacement of <see cref="ContentView"/> (which as-of .NET 8 uses Compatibility.Layout).
/// </remarks>
[ContentProperty(nameof(Content))]
public class Component : ComponentBase
{
    /// <summary>
    /// Bindable property for <see cref="Content"/> property.
    /// </summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(IView), typeof(Component), propertyChanged: OnContentPropertyChanged);

    /// <summary>
    /// Gets or sets the content of the layout.
    /// </summary>
    public IView? Content
    {
        get => (IView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc />
    protected override IView? GetContent() => Content;

    /// <inheritdoc />
    protected override void SetContent(IView? content) => Content = content;

    private static void OnContentPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((Component)bindable).OnContentPropertyChanged((IView?)oldValue, (IView?)newValue);
}
