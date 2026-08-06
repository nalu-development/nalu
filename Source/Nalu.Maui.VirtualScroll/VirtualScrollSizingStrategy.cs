using System.ComponentModel;
using System.Globalization;

namespace Nalu;

/// <summary>
/// How a <see cref="VirtualScroll" /> sizes itself along its scrolling axis.
/// </summary>
public enum VirtualScrollSizingMode
{
    /// <summary>
    /// Takes the size offered by the parent without ever measuring the content (the default).
    /// </summary>
    Fill,

    /// <summary>
    /// Hugs the content up to <see cref="VirtualScrollSizingStrategy.MaxExtent" />, then stops growing.
    /// </summary>
    Max,

    /// <summary>
    /// Hugs the content with no limit.
    /// </summary>
    Unbounded
}

/// <summary>
/// The sizing strategy of a <see cref="VirtualScroll" /> along its scrolling axis (the cross axis
/// always follows the parent's constraint — a virtualized list cannot know the size of items it
/// has not realized).
/// </summary>
/// <remarks>
/// <para>
/// The three modes exist because measuring virtualized content is not free, so the cost is opt-in:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="Fill" /> measures nothing at all — the content size is never consulted, which is why
/// it stays the default.
/// </item>
/// <item>
/// <see cref="Max(double)" /> is bounded by construction: only the items that fit within the cap
/// participate, no matter how many the collection holds. Once the content reaches the cap the
/// measured size is pinned there, so further content changes cannot move the parent and no
/// re-measure is requested at all.
/// </item>
/// <item>
/// <see cref="Unbounded" /> has no such damper: the whole content is laid out, and every content
/// change re-measures. Use it for small, bounded collections.
/// </item>
/// </list>
/// <para>
/// Not supported on Windows, where the value is accepted but always behaves as <see cref="Fill" />.
/// </para>
/// </remarks>
[TypeConverter(typeof(VirtualScrollSizingStrategyTypeConverter))]
public readonly record struct VirtualScrollSizingStrategy
{
    // Stored as 0 for every mode but Max so that `default(VirtualScrollSizingStrategy)` compares
    // equal to Fill — a struct field, array slot or unset bindable value must not produce a
    // value that behaves like Fill yet fails `== Fill`.
    private readonly double _maxExtent;

    /// <summary>Gets the sizing mode.</summary>
    public VirtualScrollSizingMode Mode { get; }

    /// <summary>
    /// Gets the maximum extent the content may grow to, in device-independent units. Only
    /// meaningful for <see cref="VirtualScrollSizingMode.Max" />; <see cref="double.PositiveInfinity" />
    /// for the other modes.
    /// </summary>
    public double MaxExtent => Mode == VirtualScrollSizingMode.Max ? _maxExtent : double.PositiveInfinity;

    private VirtualScrollSizingStrategy(VirtualScrollSizingMode mode, double maxExtent)
    {
        Mode = mode;
        _maxExtent = maxExtent;
    }

    /// <summary>
    /// Takes the size offered by the parent without measuring the content (the default).
    /// </summary>
    public static VirtualScrollSizingStrategy Fill { get; } = new(VirtualScrollSizingMode.Fill, 0);

    /// <summary>
    /// Hugs the content with no limit — measures every item, and re-measures on every content
    /// change. Intended for small, bounded collections.
    /// </summary>
    public static VirtualScrollSizingStrategy Unbounded { get; } = new(VirtualScrollSizingMode.Unbounded, 0);

    /// <summary>
    /// Hugs the content up to <paramref name="maxExtent" /> device-independent units along the
    /// scrolling axis, then stops growing.
    /// </summary>
    /// <param name="maxExtent">The maximum extent; must be a positive, finite number.</param>
    /// <exception cref="ArgumentOutOfRangeException">The extent is not a positive, finite number.</exception>
    public static VirtualScrollSizingStrategy Max(double maxExtent)
    {
        if (double.IsNaN(maxExtent) || maxExtent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExtent), maxExtent, "The maximum extent must be a positive number.");
        }

        return double.IsPositiveInfinity(maxExtent)
            ? Unbounded
            : new VirtualScrollSizingStrategy(VirtualScrollSizingMode.Max, maxExtent);
    }

    /// <summary>
    /// Converts a positive number to a <see cref="Max(double)" /> strategy (and
    /// <see cref="double.PositiveInfinity" /> to <see cref="Unbounded" />).
    /// </summary>
    /// <param name="maxExtent">The maximum extent.</param>
    public static implicit operator VirtualScrollSizingStrategy(double maxExtent) => Max(maxExtent);

    /// <summary>
    /// Converts a string representation — <c>"Fill"</c>, <c>"Unbounded"</c> or a number (which
    /// becomes <see cref="Max(double)" />) — to a <see cref="VirtualScrollSizingStrategy" />.
    /// </summary>
    /// <param name="inputString">The string representation.</param>
    /// <exception cref="FormatException">The string is not a known mode nor a valid number.</exception>
    public static implicit operator VirtualScrollSizingStrategy(string? inputString)
    {
        var value = inputString?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return Fill;
        }

        if (string.Equals(value, nameof(Fill), StringComparison.OrdinalIgnoreCase))
        {
            return Fill;
        }

        if (string.Equals(value, nameof(Unbounded), StringComparison.OrdinalIgnoreCase))
        {
            return Unbounded;
        }

        // A bare number is the Max extent: it is the only mode carrying a value, so there is
        // nothing to disambiguate. Invariant culture only — XAML is not localized.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxExtent))
        {
            return Max(maxExtent);
        }

        throw new FormatException($"'{inputString}' is not a valid {nameof(VirtualScrollSizingStrategy)}: expected '{nameof(Fill)}', '{nameof(Unbounded)}' or a positive number.");
    }

    /// <inheritdoc />
    public override string ToString()
        => Mode switch
        {
            VirtualScrollSizingMode.Max => MaxExtent.ToString(CultureInfo.InvariantCulture),
            VirtualScrollSizingMode.Unbounded => nameof(Unbounded),
            _ => nameof(Fill)
        };
}

/// <summary>
/// Type converter for <see cref="VirtualScrollSizingStrategy" />.
/// </summary>
public class VirtualScrollSizingStrategyTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string stringValue ? (VirtualScrollSizingStrategy) stringValue : null;
}
