namespace Nalu;

using System.Reflection;

/// <summary>
/// Defines an alternative navigation segment name (by default matches the name of the class).
/// </summary>
/// <param name="segmentName">The navigation segment name.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class NavigationSegmentAttribute(string segmentName) : Attribute
{
    private static readonly Dictionary<Type, string> _typeSegmentNames = [];

    /// <summary>
    /// Gets the navigation segment name.
    /// </summary>
    public string SegmentName => segmentName;

    /// <summary>
    /// Gets the segment name for the given <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The given type.</param>
    /// <returns>The segment name, defaults to type name.</returns>
    public static string GetSegmentName(Type type)
    {
        if (!_typeSegmentNames.TryGetValue(type, out var segmentName))
        {
            segmentName = type.GetCustomAttribute<NavigationSegmentAttribute>()?.SegmentName ?? type.Name;
            _typeSegmentNames.Add(type, segmentName);
        }

        return segmentName;
    }
}
