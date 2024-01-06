namespace Nalu;

using System.ComponentModel;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Represents a segment in a navigation path for the given <typeparamref name="TPageModel"/>.
/// </summary>
/// <typeparam name="TPageModel">The page model representing the navigation segment.</typeparam>
public class NavigationSegment<TPageModel> : NavigationSegment
    where TPageModel : INotifyPropertyChanged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationSegment{TPageModel}"/> class.
    /// </summary>
    public NavigationSegment()
        : base(typeof(TPageModel))
    {
    }
}

/// <summary>
/// Represents a segment in a navigation path.
/// </summary>
#pragma warning disable CA1036
public class NavigationSegment : IEquatable<NavigationSegment>, IComparable<NavigationSegment>
{
    /// <summary>
    /// Gets the segment content.
    /// </summary>
    public string Segment { get; }

    /// <summary>
    /// Gets page model type used to create this segment which implements <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public Type? PageModelType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationSegment"/> class.
    /// </summary>
    /// <param name="type">Page mode type which implements <see cref="INotifyPropertyChanged"/>.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="type"/> does not implement <see cref="INotifyPropertyChanged"/>.</exception>
    internal NavigationSegment(Type type)
    {
        if (!typeof(INotifyPropertyChanged).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type {type} must implement {nameof(INotifyPropertyChanged)}.");
        }

        PageModelType = type;
        Segment = NavigationHelper.GetSegmentName(type);
    }

    internal NavigationSegment(string segment)
    {
        Segment = segment;
    }

    /// <summary>
    /// Converts a page model type to a navigation segment.
    /// </summary>
    /// <param name="type">Page mode type which implements <see cref="INotifyPropertyChanged"/>.</param>
    public static implicit operator NavigationSegment(Type type) => new(type);

    /// <summary>
    /// Converts a navigation segment to a string.
    /// </summary>
    /// <param name="segment">The navigation segment.</param>
    public static implicit operator string(NavigationSegment segment) => segment.Segment;

    /// <summary>
    /// Verifies whether two navigation segments are equal.
    /// </summary>
    /// <param name="other">The other navigation segment to compare.</param>
    public bool Equals(NavigationSegment? other) => ReferenceEquals(this, other) || other?.Segment == Segment;

    /// <summary>
    /// Compares two navigation segments.
    /// </summary>
    /// <param name="other">The other navigation segment to compare.</param>
    public int CompareTo(NavigationSegment? other) => other is null ? 1 : string.Compare(Segment, other.Segment, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NavigationSegment segment && Equals(segment);

    /// <summary>
    /// Gets the hash code for the navigation segment.
    /// </summary>
    public override int GetHashCode() => Segment.GetHashCode();

    /// <summary>
    /// Gets the string representation of the navigation segment.
    /// </summary>
    public override string ToString() => Segment;
}
