namespace Nalu;

/// <summary>
/// Event arguments for the scroll events of a <see cref="ScrollBox" />.
/// </summary>
public class ScrollBoxScrolledEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScrollBoxScrolledEventArgs" /> class.
    /// </summary>
    /// <param name="scrollX">The current horizontal scroll position in device-independent units.</param>
    /// <param name="scrollY">The current vertical scroll position in device-independent units.</param>
    /// <param name="totalScrollableWidth">The total scrollable width in device-independent units.</param>
    /// <param name="totalScrollableHeight">The total scrollable height in device-independent units.</param>
    /// <param name="viewportWidth">The width of the viewport in device-independent units.</param>
    /// <param name="viewportHeight">The height of the viewport in device-independent units.</param>
    public ScrollBoxScrolledEventArgs(
        double scrollX,
        double scrollY,
        double totalScrollableWidth,
        double totalScrollableHeight,
        double viewportWidth,
        double viewportHeight
    )
    {
        ScrollX = scrollX;
        ScrollY = scrollY;
        TotalScrollableWidth = totalScrollableWidth;
        TotalScrollableHeight = totalScrollableHeight;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
    }

    /// <summary>
    /// Gets the current horizontal scroll position in device-independent units, measured as the
    /// distance scrolled from the start of the content.
    /// </summary>
    public double ScrollX { get; }

    /// <summary>
    /// Gets the current vertical scroll position in device-independent units, measured as the
    /// distance scrolled from the start of the content.
    /// </summary>
    public double ScrollY { get; }

    /// <summary>
    /// Gets the total scrollable width in device-independent units (including any safe-area
    /// insets the platform reserves around the content).
    /// </summary>
    public double TotalScrollableWidth { get; }

    /// <summary>
    /// Gets the total scrollable height in device-independent units (including any safe-area
    /// insets the platform reserves around the content).
    /// </summary>
    public double TotalScrollableHeight { get; }

    /// <summary>
    /// Gets the width of the viewport in device-independent units.
    /// </summary>
    public double ViewportWidth { get; }

    /// <summary>
    /// Gets the height of the viewport in device-independent units.
    /// </summary>
    public double ViewportHeight { get; }

    /// <summary>
    /// Gets the horizontal scroll percentage (0.0 to 1.0), or 1.0 if not scrollable horizontally.
    /// </summary>
    public double ScrollPercentageX => GetScrollPercentage(ScrollX, ViewportWidth, TotalScrollableWidth);

    /// <summary>
    /// Gets the vertical scroll percentage (0.0 to 1.0), or 1.0 if not scrollable vertically.
    /// </summary>
    public double ScrollPercentageY => GetScrollPercentage(ScrollY, ViewportHeight, TotalScrollableHeight);

    /// <summary>
    /// Gets the remaining horizontal scroll distance in device-independent units.
    /// </summary>
    /// <remarks>
    /// Will be 0 if already at the end or if not scrollable horizontally.
    /// </remarks>
    public double RemainingScrollX => Math.Max(TotalScrollableWidth - ViewportWidth - ScrollX, 0);

    /// <summary>
    /// Gets the remaining vertical scroll distance in device-independent units.
    /// </summary>
    /// <remarks>
    /// Will be 0 if already at the end or if not scrollable vertically.
    /// </remarks>
    public double RemainingScrollY => Math.Max(TotalScrollableHeight - ViewportHeight - ScrollY, 0);

    private static double GetScrollPercentage(double scroll, double viewportSize, double totalScrollableSize)
    {
        var scrollDistance = totalScrollableSize - viewportSize;

        return scrollDistance <= 0 ? 1.0 : Math.Clamp(scroll / scrollDistance, 0.0, 1.0);
    }
}
