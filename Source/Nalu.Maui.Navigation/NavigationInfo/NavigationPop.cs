namespace Nalu;

/// <summary>
/// Represents a segment in a navigation path which navigates to the previous page in the stack.
/// </summary>
public class NavigationPop : INavigationSegment
{
    /// <summary>
    /// Defines the segment content of a Pop navigation.
    /// </summary>
    public const string PopRoute = "..";

    /// <inheritdoc cref="INavigationSegment.SegmentName"/>
    public string SegmentName => PopRoute;

    /// <inheritdoc cref="INavigationSegment.Type"/>
    public Type? Type => null;

    /// <inheritdoc cref="INavigationSegment.AssertValid"/>
    public void AssertValid()
    {
    }
}
