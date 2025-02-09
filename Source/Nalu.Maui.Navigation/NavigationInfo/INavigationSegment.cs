namespace Nalu;

/// <summary>
/// Represents a segment in a navigation path.
/// </summary>
public interface INavigationSegment
{
    /// <summary>
    /// Gets the navigation route segment name.
    /// </summary>
    string? SegmentName { get; }

    /// <summary>
    /// Gets the type corresponding to the segment.
    /// </summary>
    /// <remarks>
    /// Can be <see langword="null" /> if the segment is a pop segment.
    /// </remarks>
    Type? Type { get; }

    /// <summary>
    /// Throws an exception if the segment is not valid.
    /// </summary>
    void AssertValid();
}
