namespace Nalu;

/// <summary>
/// A <see cref="Component"/> that uses a <see cref="DataTemplate"/> to render content.
/// </summary>
public class TemplatedComponent : TemplatedComponentBase
{
    /// <summary>
    /// Bindable property for <see cref="ContentTemplate"/> property.
    /// </summary>
    public static readonly BindableProperty ContentTemplateProperty = BindableProperty.Create(
        nameof(ContentTemplate),
        typeof(DataTemplate),
        typeof(TemplatedComponent),
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
        if (bindable is TemplatedComponent templateLayout)
        {
            templateLayout.SetTemplate(newvalue as DataTemplate);
        }
    }
}
