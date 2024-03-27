namespace Nalu;

using Microsoft.Maui.Layouts;

/// <summary>
/// Allows you to create complex layouts with a flat view hierarchy.
/// </summary>
public class ConstraintLayout : Layout, IConstraintLayout
{
    /// <summary>
    /// Identifies the <see cref="Scene"/> bindable property.
    /// </summary>
    public static readonly BindableProperty SceneProperty = BindableProperty.Create(
        nameof(Scene),
        typeof(IConstraintLayoutScene),
        typeof(ConstraintLayout),
        default(IConstraintLayoutScene),
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            if (bindable is ConstraintLayout layout)
            {
                var oldScene = (IConstraintLayoutScene?)oldValue;
                var newScene = (IConstraintLayoutScene?)newValue;

                oldScene?.SetLayout(null);
                newScene?.SetLayout(layout);
            }
        });

    private readonly Dictionary<string, IView> _views = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the scene for the layout.
    /// </summary>
    public IConstraintLayoutScene? Scene
    {
        get => (IConstraintLayoutScene)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <inheritdoc />
    public IView? GetView(string sceneId) => _views.GetValueOrDefault(sceneId);

    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new ConstraintLayoutManager(this);

    /// <inheritdoc />
    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);

        // TODO: Use a new attached property to store the scene id and cascade to AutomationId.
        _views[child.AutomationId] = (IView)child;
    }

    /// <inheritdoc />
    protected override void OnChildRemoved(Element child, int oldLogicalIndex)
    {
        base.OnChildRemoved(child, oldLogicalIndex);

        // TODO: Use a new attached property to store the scene id and cascade to AutomationId.
        _views.Remove(child.AutomationId);
    }
}
