using System.Reflection;

namespace Nalu;

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
    /// Gets the segment name for the given <paramref name="type" />.
    /// </summary>
    /// <param name="type">The given type.</param>
    /// <returns>The segment name, defaults to type name.</returns>
    public static string GetSegmentName(Type type)
    {
        if (!_typeSegmentNames.TryGetValue(type, out var segmentName))
        {
            segmentName = type.GetCustomAttribute<NavigationSegmentAttribute>()?.SegmentName ?? GetTypeName(type);
            _typeSegmentNames.Add(type, segmentName);
        }

        return segmentName;
    }

    private static string GetTypeName(Type type)
    {
        var name = type.Name;

        if (!type.IsGenericType)
        {
            return name;
        }

        var index = name.IndexOf('`');
        name = name[..index];
        var genericArguments = type.GetGenericArguments();
        var genericArgumentsNames = genericArguments.Select(GetTypeName);

        return $"{name}-{string.Join("-", genericArgumentsNames)}";
    }
}
