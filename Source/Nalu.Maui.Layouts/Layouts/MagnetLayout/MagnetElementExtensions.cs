using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// Extension methods for <see cref="IMagnetElement" />.
/// </summary>
public static class MagnetElementExtensions
{
    /// <summary>
    /// Gets the pole <see cref="Variable" /> of a magnet element.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Invalid pole type</exception>
    /// <exception cref="InvalidOperationException">The element does not implement the required interface for the specified pole</exception>
    public static Variable GetPole(this IMagnetElementBase element, HorizontalPoles pole)
        => pole switch
        {
            HorizontalPoles.Left => (element as IHorizontalPoles)?.Left ?? ThrowHorizontalPoleException(element),
            HorizontalPoles.Right => (element as IHorizontalPoles)?.Right ?? ThrowHorizontalPoleException(element),
            _ => throw new ArgumentOutOfRangeException(nameof(pole), pole, null)
        };

    /// <summary>
    /// Gets the pole <see cref="Variable" /> of a magnet element.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Invalid pole type</exception>
    /// <exception cref="InvalidOperationException">The element does not implement the required interface for the specified pole</exception>
    public static Variable GetChainPole(this IMagnetElementBase element, HorizontalPoles pole)
        => pole switch
        {
            HorizontalPoles.Left => (element as IHorizontalChainPoles)?.ChainLeft ?? ThrowHorizontalPoleException(element),
            HorizontalPoles.Right => (element as IHorizontalChainPoles)?.ChainRight ?? ThrowHorizontalPoleException(element),
            _ => throw new ArgumentOutOfRangeException(nameof(pole), pole, null)
        };

    /// <summary>
    /// Gets the pole <see cref="Variable" /> of a magnet element.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Invalid pole type</exception>
    /// <exception cref="InvalidOperationException">The element does not implement the required interface for the specified pole</exception>
    public static Variable GetPole(this IMagnetElementBase element, VerticalPoles pole)
        => pole switch
        {
            VerticalPoles.Top => (element as IVerticalPoles)?.Top ?? ThrowVerticalPoleException(element),
            VerticalPoles.Bottom => (element as IVerticalPoles)?.Bottom ?? ThrowVerticalPoleException(element),
            _ => throw new ArgumentOutOfRangeException(nameof(pole), pole, null)
        };

    /// <summary>
    /// Gets the pole <see cref="Variable" /> of a magnet element.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Invalid pole type</exception>
    /// <exception cref="InvalidOperationException">The element does not implement the required interface for the specified pole</exception>
    public static Variable GetChainPole(this IMagnetElementBase element, VerticalPoles pole)
        => pole switch
        {
            VerticalPoles.Top => (element as IVerticalChainPoles)?.ChainTop ?? ThrowVerticalPoleException(element),
            VerticalPoles.Bottom => (element as IVerticalChainPoles)?.ChainBottom ?? ThrowVerticalPoleException(element),
            _ => throw new ArgumentOutOfRangeException(nameof(pole), pole, null)
        };

    /// <summary>
    /// Gets the element of a <see cref="HorizontalPullTarget" /> in a magnet stage.
    /// </summary>
    public static IMagnetElementBase GetElement(this HorizontalPullTarget target, IMagnetStage stage)
        => stage.GetElement(target.Id);

    /// <summary>
    /// Gets the element of a <see cref="VerticalPullTarget" /> in a magnet stage.
    /// </summary>
    public static IMagnetElementBase GetElement(this VerticalPullTarget target, IMagnetStage stage)
        => stage.GetElement(target.Id);

    /// <summary>
    /// Gets the frame to which the view will be arranged.
    /// </summary>
    public static Rect GetFrame(this IMagnetView view)
        => Rect.FromLTRB(
            view.Left,
            view.Top,
            view.Right,
            view.Bottom
        );

    private static Variable ThrowVerticalPoleException(IMagnetElementBase element) => throw new InvalidOperationException($"{element.Id} does not implement IVerticalPoles");

    private static Variable ThrowHorizontalPoleException(IMagnetElementBase element) => throw new InvalidOperationException($"{element.Id} does not implement IHorizontalPoles");
}
