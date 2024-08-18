namespace Nalu;

/// <summary>
/// A <see cref="ContentLayout"/> base class that uses a <see cref="DataTemplate"/> to render content.
/// </summary>
public abstract class TemplateLayoutBase : ContentLayout
{
    private bool _changingTemplate;

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
                Content = null;
                return;
            }

            Template = dataTemplate;

            var bindingContext = IsSet(ContentBindingContextProperty) ? ContentBindingContext : BindingContext;
            var dataTemplateSelector = dataTemplate as DataTemplateSelector;
            if (bindingContext is null && dataTemplateSelector != null)
            {
                ActualTemplate = null;
                Content = null;
                return;
            }

            var actualTemplate = dataTemplateSelector?.SelectTemplate(bindingContext, this) ?? dataTemplate;

            if (actualTemplate == ActualTemplate)
            {
                if (Content is View currentContent && currentContent.BindingContext != bindingContext)
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

            Content = content;
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

        if (oldView != null)
        {
            if (oldView is BindableObject bindableObject)
            {
                bindableObject.BindingContext = null;
            }

            Remove(oldView);
        }

        if (newView != null)
        {
            Add(newView);
        }
    }
}
