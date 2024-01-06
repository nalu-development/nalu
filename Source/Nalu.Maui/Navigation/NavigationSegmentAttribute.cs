namespace Nalu;

/// <summary>
/// Defines an alternative navigation segment name (by default matches the name of the class).
/// </summary>
/// <param name="segmentName">The navigation segment name.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class NavigationSegmentAttribute(string segmentName) : Attribute
{
    /// <summary>
    /// Gets the navigation segment name.
    /// </summary>
    public string SegmentName => segmentName;
}
