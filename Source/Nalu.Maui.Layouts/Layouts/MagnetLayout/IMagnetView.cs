namespace Nalu.MagnetLayout;

/// <summary>
/// Represents a real view in the magnet layout.
/// </summary>
public interface IMagnetView : IMagnetElement
{
    /// <summary>
    /// Gets the visibility of the view.
    /// </summary>
    bool Collapsed { get; }

    /// <summary>
    /// Gets the top position of the view.
    /// </summary>
    double Top { get; }

    /// <summary>
    /// Gets the bottom position of the view.
    /// </summary>
    double Bottom { get; }
    
    /// <summary>
    /// Gets the left position of the view.
    /// </summary>
    double Left { get; }

    /// <summary>
    /// Gets the right position of the view.
    /// </summary>
    double Right { get; }

    /// <summary>
    /// Gets the effective margin of the view.
    /// </summary>
    /// <returns></returns>
    Thickness GetEffectiveMargin();
}
