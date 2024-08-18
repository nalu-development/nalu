namespace Nalu;

using Microsoft.Maui.Layouts;

/// <summary>
/// ContentLayout is a layout that is used to display a single view.
/// </summary>
/// <remarks>
/// Can be used as a replacement of <see cref="ContentView"/> (which as-of .NET 8 uses Compatibility.Layout).
/// </remarks>
[ContentProperty(nameof(Content))]
public class ContentLayout : Layout
{
    /// <summary>
    /// Bindable property for <see cref="Content"/> property.
    /// </summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(IView), typeof(ContentLayout), propertyChanged: OnContentPropertyChanged);

    /// <summary>
    /// Bindable property for <see cref="ContentBindingContext"/> property.
    /// </summary>
    public static readonly BindableProperty ContentBindingContextProperty = BindableProperty.Create(
        nameof(ContentBindingContext),
        typeof(object),
        typeof(ContentLayout),
        propertyChanged: ContentBindingContextPropertyChanged);

    /// <summary>
    /// Gets or sets the <see cref="BindableObject.BindingContext"/> to force on the content view.
    /// </summary>
    /// <remarks>
    /// This helps to fulfill interface segregation principle by allowing the content view to be bound
    /// to a property of the parent's binding context.
    /// </remarks>
    /// <example>
    /// <code>
    /// <![CDATA[
    ///     <ContentLayout ContentBindingContext="{Binding CurrentAnimal}">
    ///         <AnimalView x:DataType="models:Animal" />
    ///     </ContentLayout>
    /// ]]>
    /// </code>
    /// </example>
    public object? ContentBindingContext
    {
        get => GetValue(ContentBindingContextProperty);
        set => SetValue(ContentBindingContextProperty, value);
    }

    /// <summary>
    /// Gets or sets the content of the layout.
    /// </summary>
    public IView? Content
    {
        get => (IView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new ContentLayoutLayoutManager(this);

    /// <summary>
    /// Sets the content of the layout.
    /// </summary>
    /// <param name="oldView">The old content.</param>
    /// <param name="newView">The new content.</param>
    protected virtual void OnContentPropertyChanged(IView? oldView, IView? newView)
    {
        if (oldView != null)
        {
            if (IsSet(ContentBindingContextProperty) && oldView is BindableObject bindableView)
            {
                bindableView.BindingContext = null;
            }

            Remove(oldView);
        }

        if (newView != null)
        {
            if (IsSet(ContentBindingContextProperty) && newView is BindableObject bindableView)
            {
                bindableView.BindingContext = ContentBindingContext;
            }

            Add(newView);
        }
    }

    /// <summary>
    /// Updates the binding context of the content view.
    /// </summary>
    /// <param name="oldvalue">The old BindingContext.</param>
    /// <param name="newvalue">The new BindingContext.</param>
    protected virtual void ContentBindingContextPropertyChanged(object? oldvalue, object? newvalue)
    {
        if (Content is BindableObject bindableView)
        {
            bindableView.BindingContext = newvalue;
        }
    }

    private static void OnContentPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is ContentLayout contentLayout)
        {
            contentLayout.OnContentPropertyChanged(oldValue as IView, newValue as IView);
        }
    }

    private static void ContentBindingContextPropertyChanged(BindableObject bindable, object? oldvalue, object? newvalue)
    {
        if (bindable is ContentLayout contentLayout)
        {
            contentLayout.ContentBindingContextPropertyChanged(oldvalue, newvalue);
        }
    }
}
