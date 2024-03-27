namespace Nalu;

using ILayout = Microsoft.Maui.ILayout;

/// <summary>
/// Allows you to create complex layouts with a flat view hierarchy.
/// </summary>
public interface IConstraintLayout : ILayout
{
    /// <summary>
    /// Gets the scene for the layout.
    /// </summary>
    IConstraintLayoutScene? Scene { get; }

    /// <summary>
    /// Gets a view by its scene id.
    /// </summary>
    /// <param name="sceneId">The scene identifier for the view.</param>
    IView? GetView(string sceneId);
}
