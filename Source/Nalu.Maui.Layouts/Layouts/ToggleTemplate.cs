namespace Nalu;

/// <summary>
/// A <see cref="ViewBox"/> that uses a <see cref="DataTemplate"/> to render content based on a boolean value.
/// </summary>
public partial class ToggleTemplate : TemplateBoxBase
{
    /// <summary>
    /// Bindable property for <see cref="WhenTrue"/> property.
    /// </summary>
    public static readonly BindableProperty WhenTrueProperty =
        BindableProperty.Create(nameof(WhenTrue), typeof(DataTemplate), typeof(ToggleTemplate), propertyChanged: ConditionChanged);

    /// <summary>
    /// Bindable property for <see cref="WhenFalse"/> property.
    /// </summary>
    public static readonly BindableProperty WhenFalseProperty =
        BindableProperty.Create(nameof(WhenFalse), typeof(DataTemplate), typeof(ToggleTemplate), propertyChanged: ConditionChanged);

    /// <summary>
    /// Bindable property for <see cref="Value"/> property.
    /// </summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(bool?), typeof(ToggleTemplate), propertyChanged: ConditionChanged);

    /// <summary>
    /// Gets or sets the <see cref="DataTemplate"/> to use when the value is false.
    /// </summary>
    public DataTemplate? WhenFalse
    {
        get => (DataTemplate?)GetValue(WhenFalseProperty);
        set => SetValue(WhenFalseProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="DataTemplate"/> to use when the value is true.
    /// </summary>
    public DataTemplate? WhenTrue
    {
        get => (DataTemplate?)GetValue(WhenTrueProperty);
        set => SetValue(WhenTrueProperty, value);
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
            true => WhenTrue,
            false => WhenFalse,
            _ => null,
        };

        SetTemplate(template);
    }

    private static void ConditionChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is ToggleTemplate toggleTemplate)
        {
            toggleTemplate.SetTemplateFromValue();
        }
    }
}
