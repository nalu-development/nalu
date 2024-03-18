namespace Nalu;

using System.ComponentModel;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Represents a segment in a navigation path.
/// </summary>
#pragma warning disable CA1036
public class NavigationSegment : BindableObject, INavigationSegment
{
    /// <summary>
    /// Defines the segment property.
    /// </summary>
    public static readonly BindableProperty RouteProperty = BindableProperty.Create(
        nameof(SegmentName),
        typeof(string),
        typeof(NavigationSegment),
        propertyChanging: (_, newvalue, _) =>
        {
            var route = (string?)newvalue;
            if (route is not null)
            {
                if (route.Contains('/'))
                {
                    throw new InvalidOperationException("Route cannot contain '/' character.");
                }

                if (route != NavigationPop.PopRoute && route.Contains(NavigationPop.PopRoute))
                {
                    throw new InvalidOperationException($"Route cannot contain '{NavigationPop.PopRoute}'.");
                }
            }
        });

    /// <summary>
    /// Defines the page model type property.
    /// </summary>
    public static readonly BindableProperty TypeProperty = BindableProperty.Create(
        nameof(Type),
        typeof(Type),
        typeof(NavigationSegment));

    /// <summary>
    /// Gets or sets the segment content.
    /// </summary>
    public string? SegmentName
    {
        get => (string?)GetValue(RouteProperty);
        set => SetValue(RouteProperty, value);
    }

    /// <summary>
    /// Gets or sets type to use on navigation.
    /// </summary>
    [TypeConverter(typeof(TypeTypeConverter))]
    public Type? Type
    {
        get => (Type?)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    /// <inheritdoc cref="INavigationSegment.AssertValid"/>
    public void AssertValid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SegmentName, nameof(SegmentName));
        ArgumentNullException.ThrowIfNull(Type, nameof(Type));
    }

    /// <summary>
    /// Converts a page model type to a navigation segment.
    /// </summary>
    /// <param name="type">Page mode type which implements <see cref="INotifyPropertyChanged"/>.</param>
    public static implicit operator NavigationSegment(Type type) => new()
    {
        Type = type,
    };

    /// <summary>
    /// Converts a navigation segment to a string.
    /// </summary>
    /// <param name="segment">The navigation segment.</param>
    public static implicit operator string(NavigationSegment segment) => segment.ToString();

    /// <summary>
    /// Gets the string representation of the navigation segment.
    /// </summary>
    public override string ToString() => SegmentName ?? string.Empty;
}
