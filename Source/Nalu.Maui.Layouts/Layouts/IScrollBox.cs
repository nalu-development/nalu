namespace Nalu;

/// <summary>
/// Handler-facing contract of the <see cref="ScrollBox" /> view.
/// </summary>
public interface IScrollBox : IViewBox
{
    /// <summary>
    /// Gets the scrolling axis.
    /// </summary>
    ScrollBoxOrientation Orientation { get; }

    /// <summary>
    /// Gets a value indicating whether user scroll gestures are enabled
    /// (programmatic scrolling always works).
    /// </summary>
    bool IsScrollEnabled { get; }

    /// <summary>
    /// Gets the scroll bar visibility along the scrolling axis.
    /// </summary>
    ScrollBarVisibility ScrollBarVisibility { get; }

    /// <summary>
    /// Gets the length of the fading edge effect in device-independent units (0 disables it).
    /// </summary>
    double FadingEdgeLength { get; }

    /// <summary>
    /// Gets how the box sizes itself along its scrolling axis.
    /// </summary>
    ScrollBoxSizingStrategy SizingStrategy { get; }

    /// <summary>
    /// Gets a value indicating whether pull-to-refresh is enabled.
    /// </summary>
    bool IsRefreshEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether the refresh indicator is currently showing.
    /// </summary>
    bool IsRefreshing { get; }

    /// <summary>
    /// Gets the accent color for the refresh indicator.
    /// </summary>
    Color? RefreshAccentColor { get; }
}

/// <summary>
/// Platform-to-control callback channel used by the <see cref="ScrollBox" /> handler.
/// </summary>
internal interface IScrollBoxController
{
    /// <summary>
    /// Invoked when the user triggers a pull-to-refresh gesture.
    /// </summary>
    /// <param name="completionCallback">Callback the platform receives to end the refresh indicator.</param>
    void Refresh(Action completionCallback);

    /// <summary>
    /// Invoked on every scroll position change while scroll events are enabled.
    /// </summary>
    void Scrolled(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight);

    /// <summary>
    /// Invoked when a user-initiated scroll gesture starts moving the content.
    /// </summary>
    void ScrollStarted(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight);

    /// <summary>
    /// Invoked when scrolling comes to rest after a user-initiated gesture.
    /// </summary>
    void ScrollEnded(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight);

    /// <summary>
    /// Invoked after every platform layout pass of the scrollable content, with the size of the
    /// scrollable canvas in device-independent units.
    /// </summary>
    void ContentLaidOut(double contentWidth, double contentHeight);
}
