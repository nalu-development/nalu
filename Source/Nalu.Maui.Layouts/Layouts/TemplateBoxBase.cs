namespace Nalu;

using Nalu.Internals;

/// <summary>
/// A <see cref="ViewBox"/> base class that uses a <see cref="DataTemplate"/> to render content.
/// </summary>
[ContentProperty(nameof(TemplateContent))]
public abstract class TemplateBoxBase : ViewBoxBase
{
    /// <summary>
    /// Bindable property for <see cref="TemplateContent"/> property.
    /// </summary>
    public static readonly BindableProperty TemplateContentProperty = BindableProperty.Create(
        nameof(TemplateContent),
        typeof(IView),
        typeof(TemplateBox));

    private bool _changingTemplate;

    /// <summary>
    /// Gets or sets the content to be projected through <see cref="TemplateContentPresenter"/> component.
    /// </summary>
    public IView? TemplateContent
    {
        get => (IView?)GetValue(TemplateContentProperty);
        set => SetValue(TemplateContentProperty, value);
    }

    /// <summary>
    /// Gets the active template.
    /// </summary>
    protected DataTemplate? Template { get; private set; }

    /// <summary>
    /// Gets the current template used to render content.
    /// </summary>
    /// <remarks>
    /// Matches the <see cref="Template"/> property unless a <see cref="DataTemplateSelector"/> is used.
    /// </remarks>
    protected DataTemplate? ActualTemplate { get; private set; }

    /// <summary>
    /// Sets the template to use for rendering content.
    /// </summary>
    /// <param name="dataTemplate">The <see cref="DataTemplate"/> or <see cref="DataTemplateSelector"/> to use.</param>
    protected void SetTemplate(DataTemplate? dataTemplate)
    {
        try
        {
            _changingTemplate = true;
            if (dataTemplate == null)
            {
                ActualTemplate = null;
                SetContent(null);
                return;
            }

            Template = dataTemplate;

            var bindingContext = IsSet(ContentBindingContextProperty) ? ContentBindingContext : BindingContext;
            var dataTemplateSelector = dataTemplate as DataTemplateSelector;
            if (bindingContext is null && dataTemplateSelector != null)
            {
                ActualTemplate = null;
                SetContent(null);
                return;
            }

            var actualTemplate = dataTemplateSelector?.SelectTemplate(bindingContext, this) ?? dataTemplate;

            if (actualTemplate == ActualTemplate)
            {
                if (GetContent() is BindableObject currentContent && currentContent.BindingContext != bindingContext)
                {
                    currentContent.BindingContext = bindingContext;
                }

                return;
            }

            ActualTemplate = actualTemplate;

            var content = (IView)actualTemplate.CreateContent();
            if (content is BindableObject bindableContent)
            {
                bindableContent.BindingContext = bindingContext;
            }

            SetContent(content);
        }
        finally
        {
            _changingTemplate = false;
        }
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        SetTemplate(Template);
    }

    /// <inheritdoc />
    protected override void ContentBindingContextPropertyChanged(object? oldvalue, object? newvalue) => SetTemplate(Template);

    /// <inheritdoc />
    protected override void OnContentPropertyChanged(IView? oldView, IView? newView)
    {
        if (!_changingTemplate)
        {
            throw new InvalidOperationException($"The content of {GetType().Name} should not be set directly.");
        }

        if (newView is Element newElement)
        {
            AddLogicalChild(newElement);
        }

        Handler?.UpdateValue(nameof(IContentView.Content));

        if (oldView != null)
        {
            DisconnectHandlerHelper.DisconnectHandlers(oldView);

            if (oldView is BindableObject bindableView)
            {
                bindableView.BindingContext = null;
            }

            if (oldView is Element oldElement)
            {
                RemoveLogicalChild(oldElement);
            }
        }
    }
}
