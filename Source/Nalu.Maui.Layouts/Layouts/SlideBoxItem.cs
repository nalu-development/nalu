namespace Nalu;

/// <summary>
/// A slide of a <see cref="SlideBox" />: a lazily-realized <see cref="DataTemplate" /> with a
/// participation flag.
/// </summary>
/// <remarks>
/// The template is realized the first time the slide is presented (or becomes the neighbor of
/// an active swipe) and the created content is retained from then on, preserving its state
/// across navigation. Disabling the slide excludes it from the sequence AND tears the realized
/// content down — re-enabling rebuilds it lazily on the next visit.
/// </remarks>
[ContentProperty(nameof(Template))]
public sealed class SlideBoxItem : Element
{
    /// <summary>Bindable property for <see cref="Template" />.</summary>
    public static readonly BindableProperty TemplateProperty = BindableProperty.Create(
        nameof(Template),
        typeof(DataTemplate),
        typeof(SlideBoxItem),
        propertyChanged: static (bindable, _, _) => ((SlideBoxItem) bindable).OnTemplateChanged()
    );

    /// <summary>Bindable property for <see cref="ContentBindingContext" />.</summary>
    public static readonly BindableProperty ContentBindingContextProperty = BindableProperty.Create(
        nameof(ContentBindingContext),
        typeof(object),
        typeof(SlideBoxItem),
        propertyChanged: static (bindable, _, newvalue) => ((SlideBoxItem) bindable).OnContentBindingContextChanged(newvalue)
    );

    /// <summary>Bindable property for <see cref="IsEnabled" />.</summary>
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(
        nameof(IsEnabled),
        typeof(bool),
        typeof(SlideBoxItem),
        true,
        propertyChanged: static (bindable, _, _) => ((SlideBoxItem) bindable).OnIsEnabledChanged()
    );

    /// <summary>Gets or sets the lazy factory of the slide's content.</summary>
    public DataTemplate? Template
    {
        get => (DataTemplate?) GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the <see cref="BindableObject.BindingContext" /> to force on the realized content.
    /// </summary>
    /// <remarks>
    /// This helps to fulfill interface segregation principle by allowing the slide's content
    /// to be bound to a property of the parent's binding context. Applied when the template is
    /// realized and whenever the value changes; cleared when the content is torn down.
    /// </remarks>
    /// <example>
    ///     <code>
    /// <![CDATA[
    ///     <nalu:SlideBoxItem ContentBindingContext="{Binding CurrentAnimal}">
    ///         <DataTemplate>
    ///             <AnimalView x:DataType="models:Animal" />
    ///         </DataTemplate>
    ///     </nalu:SlideBoxItem>
    /// ]]>
    /// </code>
    /// </example>
    public object? ContentBindingContext
    {
        get => GetValue(ContentBindingContextProperty);
        set => SetValue(ContentBindingContextProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the slide participates in the sequence. Disabled slides are
    /// skipped by swipes and <see cref="SlideBox.Next" />/<see cref="SlideBox.Previous" />,
    /// and their realized content is torn down.
    /// </summary>
    public bool IsEnabled
    {
        get => (bool) GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>Gets the realized content, or null while the slide has never been presented (or is disabled).</summary>
    public View? Content { get; internal set; }

    private void OnTemplateChanged()
        => (Parent as SlideBox)?.OnItemTemplateChanged(this);

    private void OnContentBindingContextChanged(object? newvalue)
    {
        if (Content is { } content)
        {
            content.BindingContext = newvalue;
        }
    }

    private void OnIsEnabledChanged()
        => (Parent as SlideBox)?.OnItemIsEnabledChanged(this);
}
