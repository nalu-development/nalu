namespace Nalu;

using Microsoft.Maui.Layouts;

/// <summary>
/// <see cref="ViewBoxBase"/> is a base class a <see cref="IContentView"/> that is used to display a single view.
/// </summary>
public abstract class ViewBoxBase : View, IContentView
{
    private ILayoutManager? _layoutManager;
    private ILayoutManager LayoutManager => _layoutManager ??= new ViewBoxLayoutManager(this);

    /// <summary>
    /// Bindable property for <see cref="Padding"/> property.
    /// </summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding),
        typeof(Thickness),
        typeof(ViewBoxBase),
        default(Thickness),
        propertyChanged: OnPaddingPropertyChanged);

    private static void OnPaddingPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
        => ((ViewBoxBase)bindable).OnPaddingPropertyChanged((Thickness)oldvalue, (Thickness)newvalue);

    /// <summary>
    /// Bindable property for <see cref="ContentBindingContext"/> property.
    /// </summary>
    public static readonly BindableProperty ContentBindingContextProperty = BindableProperty.Create(
        nameof(ContentBindingContext),
        typeof(object),
        typeof(ViewBox),
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
    /// Gets or sets the padding around the content.
    /// </summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

#pragma warning disable IDE0032
    private IView? _contentView;
#pragma warning restore IDE0032

    Size IContentView.CrossPlatformMeasure(double widthConstraint, double heightConstraint) => LayoutManager.Measure(widthConstraint, heightConstraint);
    Size IContentView.CrossPlatformArrange(Rect bounds) => LayoutManager.ArrangeChildren(bounds);
    object? IContentView.Content => GetContent();

    /// <inheritdoc />
    public IView? PresentedContent => GetContent();

    /// <summary>
    /// Updates the binding context of the content view.
    /// </summary>
    /// <param name="oldvalue">The old BindingContext.</param>
    /// <param name="newvalue">The new BindingContext.</param>
    protected virtual void ContentBindingContextPropertyChanged(object? oldvalue, object? newvalue)
    {
        if (GetContent() is BindableObject bindableView)
        {
            bindableView.BindingContext = newvalue;
        }
    }

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

            if (oldView is Element oldElement)
            {
                RemoveLogicalChild(oldElement);
            }
        }

        if (newView != null)
        {
            if (IsSet(ContentBindingContextProperty) && newView is BindableObject bindableView)
            {
                bindableView.BindingContext = ContentBindingContext;
            }

            if (newView is Element newElement)
            {
                AddLogicalChild(newElement);
            }
        }
    }

    /// <summary>
    /// Triggered when the <see cref="Padding"/> property changes.
    /// </summary>
    /// <param name="oldValue">The old padding.</param>
    /// <param name="newValue">The new padding.</param>
    protected virtual void OnPaddingPropertyChanged(Thickness oldValue, Thickness newValue)
        => InvalidateMeasure();

    /// <summary>
    /// Gets the content.
    /// </summary>
    protected virtual IView? GetContent() => _contentView;

    /// <summary>
    /// Sets the content.
    /// </summary>
    /// <param name="content">The new content.</param>
    protected virtual void SetContent(IView? content)
    {
        var oldContent = _contentView;
        if (ReferenceEquals(oldContent, content))
        {
            return;
        }

        _contentView = content;

        OnContentPropertyChanged(oldContent, content);
        Handler?.UpdateValue(nameof(IContentView.Content));
    }

    private static void ContentBindingContextPropertyChanged(BindableObject bindable, object? oldvalue, object? newvalue)
    {
        if (bindable is ViewBoxBase viewBoxBase)
        {
            viewBoxBase.ContentBindingContextPropertyChanged(oldvalue, newvalue);
        }
    }
}
