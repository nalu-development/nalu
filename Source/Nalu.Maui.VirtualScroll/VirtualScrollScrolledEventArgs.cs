namespace Nalu;

/// <summary>
/// Event arguments for the scrolled event in VirtualScroll.
/// </summary>
public class VirtualScrollScrolledEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollScrolledEventArgs"/> class.
    /// </summary>
    /// <param name="scrollX">The current horizontal scroll position in device-independent units.</param>
    /// <param name="scrollY">The current vertical scroll position in device-independent units.</param>
    /// <param name="totalScrollableWidth">The total scrollable width in device-independent units.</param>
    /// <param name="totalScrollableHeight">The total scrollable height in device-independent units.</param>
    public VirtualScrollScrolledEventArgs(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight)
    {
        ScrollX = scrollX;
        ScrollY = scrollY;
        TotalScrollableWidth = totalScrollableWidth;
        TotalScrollableHeight = totalScrollableHeight;
    }

    /// <summary>
    /// Gets the current horizontal scroll position in device-independent units.
    /// </summary>
    public double ScrollX { get; }

    /// <summary>
    /// Gets the current vertical scroll position in device-independent units.
    /// </summary>
    public double ScrollY { get; }

    /// <summary>
    /// Gets the total scrollable width in device-independent units.
    /// </summary>
    public double TotalScrollableWidth { get; }

    /// <summary>
    /// Gets the total scrollable height in device-independent units.
    /// </summary>
    public double TotalScrollableHeight { get; }

    /// <summary>
    /// Gets the horizontal scroll percentage (0.0 to 1.0), or 0.0 if not scrollable horizontally.
    /// </summary>
    public double ScrollPercentageX => TotalScrollableWidth > 0 ? Math.Clamp(ScrollX / TotalScrollableWidth, 0.0, 1.0) : 0.0;

    /// <summary>
    /// Gets the vertical scroll percentage (0.0 to 1.0), or 0.0 if not scrollable vertically.
    /// </summary>
    public double ScrollPercentageY => TotalScrollableHeight > 0 ? Math.Clamp(ScrollY / TotalScrollableHeight, 0.0, 1.0) : 0.0;
}

