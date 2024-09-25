namespace Nalu;

/// <summary>
/// A <see cref="ViewBox"/> to display the <see cref="TemplateBoxBase.TemplateContent" />.
/// </summary>
public class TemplateContentPresenter : ViewBox
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateContentPresenter"/> class.
    /// </summary>
    public TemplateContentPresenter()
    {
        var binding = new Binding(nameof(TemplateBox.TemplateContent), source: new RelativeBindingSource(RelativeBindingSourceMode.FindAncestor, typeof(TemplateBoxBase)));
        SetBinding(ContentProperty, binding);
    }
}
