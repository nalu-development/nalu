namespace Nalu;

/// <summary>
/// Base class for virtual scroll layouts.
/// </summary>
public abstract class VirtualScrollLayout : BindableObject, IVirtualScrollLayout
{
    /// <summary>
    /// Gets the orientation of the layout.
    /// </summary>
    public ItemsLayoutOrientation Orientation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollLayout" /> class.
    /// </summary>
    /// <param name="orientation">The orientation of the layout.</param>
    protected VirtualScrollLayout(ItemsLayoutOrientation orientation)
    {
        Orientation = orientation;
    }
}

