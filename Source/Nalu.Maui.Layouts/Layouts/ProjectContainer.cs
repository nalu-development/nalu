namespace Nalu;

/// <summary>
/// A <see cref="Component"/> to display the <see cref="TemplatedComponentBase.ProjectedContent" />.
/// </summary>
public class ProjectContainer : Component
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectContainer"/> class.
    /// </summary>
    public ProjectContainer()
    {
        var binding = new Binding(nameof(TemplatedComponent.ProjectedContent), source: new RelativeBindingSource(RelativeBindingSourceMode.FindAncestor, typeof(TemplatedComponentBase)));
        SetBinding(ContentProperty, binding);
    }
}
