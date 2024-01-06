namespace Nalu;

/// <summary>
/// Represents a segment in a navigation path which navigates to the previous page in the stack.
/// </summary>
public class NavigationPop() : NavigationSegment("..")
{
    /// <summary>
    /// Defines the segment content of a Pop navigation.
    /// </summary>
    public const string PopSegment = "..";
}
