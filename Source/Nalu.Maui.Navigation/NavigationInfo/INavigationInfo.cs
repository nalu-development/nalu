namespace Nalu;

/// <summary>
/// Represents a navigation request.
/// </summary>
public interface INavigationInfo : IReadOnlyList<INavigationSegment>
{
    /// <inheritdoc cref="NavigationBehavior"/>
    NavigationBehavior? Behavior { get; }

    /// <summary>
    /// Gets a value indicating whether the navigation is absolute.
    /// </summary>
    bool IsAbsolute { get; }

    /// <summary>
    /// Gets the navigation intent.
    /// </summary>
    object? Intent { get; }

    /// <summary>
    /// Gets the path to navigate to.
    /// </summary>
    public string Path { get; }
}
