namespace Nalu;

/// <summary>
/// A <see cref="ViewBox"/> that uses a <see cref="DataTemplate"/> or <see cref="DataTemplateSelector"/> to render content.
/// </summary>
public partial class TemplateBox : TemplateBoxBase
{
    /// <summary>
    /// Bindable property for <see cref="ContentTemplate"/> property.
    /// </summary>
    public static readonly BindableProperty ContentTemplateProperty = BindableProperty.Create(
        nameof(ContentTemplate),
        typeof(DataTemplate),
        typeof(TemplateBox),
        propertyChanged: ContentTemplateChanged);

    /// <summary>
    /// Gets or sets the <see cref="DataTemplate"/> used to render the content.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    private static void ContentTemplateChanged(BindableObject bindable, object? oldvalue, object? newvalue)
    {
        if (bindable is TemplateBox templateBox)
        {
            templateBox.SetTemplate(newvalue as DataTemplate);
        }
    }
}
