namespace Nalu;

/// <summary>
/// A <see cref="ContentLayout"/> that uses a <see cref="DataTemplate"/> to render content based on a boolean value.
/// </summary>
public class ConditionalTemplateLayout : TemplateLayoutBase
{
    /// <summary>
    /// Bindable property for <see cref="TrueTemplate"/> property.
    /// </summary>
    public static readonly BindableProperty TrueTemplateProperty =
        BindableProperty.Create(nameof(TrueTemplate), typeof(DataTemplate), typeof(ConditionalTemplateLayout), propertyChanged: ConditionChanged);

    /// <summary>
    /// Bindable property for <see cref="FalseTemplate"/> property.
    /// </summary>
    public static readonly BindableProperty FalseTemplateProperty =
        BindableProperty.Create(nameof(FalseTemplate), typeof(DataTemplate), typeof(ConditionalTemplateLayout), propertyChanged: ConditionChanged);

    /// <summary>
    /// Bindable property for <see cref="Value"/> property.
    /// </summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(bool?), typeof(ConditionalTemplateLayout), propertyChanged: ConditionChanged);

    /// <summary>
    /// Gets or sets the <see cref="DataTemplate"/> to use when the value is false.
    /// </summary>
    public DataTemplate FalseTemplate
    {
        get => (DataTemplate)GetValue(FalseTemplateProperty);
        set => SetValue(FalseTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="DataTemplate"/> to use when the value is true.
    /// </summary>
    public DataTemplate TrueTemplate
    {
        get => (DataTemplate)GetValue(TrueTemplateProperty);
        set => SetValue(TrueTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the value to determine which template to use.
    /// </summary>
    public bool? Value
    {
        get => (bool?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void SetTemplateFromValue()
    {
        var template = Value switch
        {
            true => TrueTemplate,
            false => FalseTemplate,
            _ => null,
        };

        SetTemplate(template);
    }

    private static void ConditionChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ConditionalTemplateLayout chooseTemplate)
        {
            chooseTemplate.SetTemplateFromValue();
        }
    }
}
