namespace Nalu.MagnetLayout;

/// <summary>
/// An interface for elements that can be part of a magnet layout.
/// </summary>
public interface IMagnetElement
{
    /// <summary>
    /// Gets the unique identifier for this element.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Sets or unsets the magnet layout context for this element.
    /// </summary>
    /// <remarks>
    /// This method is called when the element is added or removed from the magnet layout.
    /// Constraints should be added or removed in this method.
    /// It's not necessary to invoke <see cref="IMagnetStage.Invalidate"/> here, as it is implicit.
    /// </remarks>
    void SetStage(IMagnetStage? stage);
}
