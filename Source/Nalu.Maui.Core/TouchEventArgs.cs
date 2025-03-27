namespace Nalu;

/// <summary>
/// Represents the event data for touch events.
/// </summary>
public class TouchEventArgs
{
    /// <summary>
    /// Touch position relative to the control itself in DP.
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Gets a value indicating whether the event bubbles up.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public bool Propagates { get; private set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="TouchEventArgs" /> class.
    /// </summary>
    /// <param name="position"></param>
    public TouchEventArgs(Point position)
    {
        Position = position;
    }

    /// <summary>
    /// Prevents the event from propagating (on supported platforms).
    /// </summary>
    public void StopPropagation() => Propagates = false;
}
