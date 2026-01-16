namespace Nalu;

/// <summary>
/// Represents a layout for the virtual scroll.
/// </summary>
public interface IVirtualScrollLayout
{
    /// <summary>
    /// Gets the orientation of the layout.
    /// </summary>
    ItemsLayoutOrientation Orientation { get; }
}

