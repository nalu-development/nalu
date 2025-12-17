namespace Nalu;

/// <summary>
/// A linear layout for virtual scroll that arranges items in a single line.
/// </summary>
public sealed class LinearVirtualScrollLayout : VirtualScrollLayout
{
    /// <summary>
    /// Gets a vertical linear layout.
    /// </summary>
    public static LinearVirtualScrollLayout Vertical { get; } = new(ItemsLayoutOrientation.Vertical);

    /// <summary>
    /// Gets a horizontal linear layout.
    /// </summary>
    public static LinearVirtualScrollLayout Horizontal { get; } = new(ItemsLayoutOrientation.Horizontal);

    /// <summary>
    /// Initializes a new instance of the <see cref="LinearVirtualScrollLayout" /> class.
    /// </summary>
    /// <param name="orientation">The orientation of the layout.</param>
    private LinearVirtualScrollLayout(ItemsLayoutOrientation orientation)
        : base(orientation)
    {
    }
}

