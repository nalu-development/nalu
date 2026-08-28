using System.ComponentModel;
using System.Globalization;

namespace Nalu;

/// <summary>
/// How a <see cref="ScrollBox" /> sizes itself along its scrolling axis.
/// </summary>
public enum ScrollBoxSizingMode
{
    /// <summary>
    /// Takes the size offered by the parent without ever measuring the content (the default).
    /// </summary>
    Fill,

    /// <summary>
    /// Hugs the content up to <see cref="ScrollBoxSizingStrategy.MaxExtent" />, then stops growing.
    /// </summary>
    Max,

    /// <summary>
    /// Hugs the content with no limit.
    /// </summary>
    Unbounded
}

/// <summary>
/// The sizing strategy of a <see cref="ScrollBox" /> along its scrolling axis (the cross axis
/// always follows the content measured within the parent's constraint).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Fill" /> never consults the content size and simply takes what the parent offers —
/// the predictable default for a scroll container filling a page.
/// </para>
/// <para>
/// The hugging modes (<see cref="Max(double)" /> and <see cref="Unbounded" />) measure the content
/// and size the box to it, which is what a ScrollBox inside an <c>Auto</c> grid row or a bottom
/// sheet wants: the box shrinks <b>and</b> grows with its content. Content-size changes re-measure
/// the box only when the resulting extent actually moves (with a half-unit epsilon), and the
/// invalidation is always dispatched outside the platform layout pass, so the feedback loop
/// converges by construction.
/// </para>
/// <para>
/// The naming intentionally mirrors <c>VirtualScrollSizingStrategy</c> from Nalu.Maui.VirtualScroll.
/// Unlike the virtualized control, all modes are supported on every platform here — measuring a
/// single content view is cheap.
/// </para>
/// </remarks>
[TypeConverter(typeof(ScrollBoxSizingStrategyTypeConverter))]
public readonly record struct ScrollBoxSizingStrategy
{
    // Stored as 0 for every mode but Max so that `default(ScrollBoxSizingStrategy)` compares
    // equal to Fill — a struct field, array slot or unset bindable value must not produce a
    // value that behaves like Fill yet fails `== Fill`.
    private readonly double _maxExtent;

    /// <summary>Gets the sizing mode.</summary>
    public ScrollBoxSizingMode Mode { get; }

    /// <summary>
    /// Gets the maximum extent the content may grow to, in device-independent units. Only
    /// meaningful for <see cref="ScrollBoxSizingMode.Max" />; <see cref="double.PositiveInfinity" />
    /// for the other modes.
    /// </summary>
    public double MaxExtent => Mode == ScrollBoxSizingMode.Max ? _maxExtent : double.PositiveInfinity;

    private ScrollBoxSizingStrategy(ScrollBoxSizingMode mode, double maxExtent)
    {
        Mode = mode;
        _maxExtent = maxExtent;
    }

    /// <summary>
    /// Takes the size offered by the parent without measuring the content (the default).
    /// </summary>
    public static ScrollBoxSizingStrategy Fill { get; } = new(ScrollBoxSizingMode.Fill, 0);

    /// <summary>
    /// Hugs the content with no limit: the box is exactly as large as its content along the
    /// scrolling axis (still capped by the room the parent offers).
    /// </summary>
    public static ScrollBoxSizingStrategy Unbounded { get; } = new(ScrollBoxSizingMode.Unbounded, 0);

    /// <summary>
    /// Hugs the content up to <paramref name="maxExtent" /> device-independent units along the
    /// scrolling axis, then stops growing.
    /// </summary>
    /// <param name="maxExtent">The maximum extent; must be a positive, finite number.</param>
    /// <exception cref="ArgumentOutOfRangeException">The extent is not a positive, finite number.</exception>
    public static ScrollBoxSizingStrategy Max(double maxExtent)
    {
        if (double.IsNaN(maxExtent) || maxExtent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExtent), maxExtent, "The maximum extent must be a positive number.");
        }

        return double.IsPositiveInfinity(maxExtent)
            ? Unbounded
            : new ScrollBoxSizingStrategy(ScrollBoxSizingMode.Max, maxExtent);
    }

    /// <summary>
    /// Converts a positive number to a <see cref="Max(double)" /> strategy (and
    /// <see cref="double.PositiveInfinity" /> to <see cref="Unbounded" />).
    /// </summary>
    /// <param name="maxExtent">The maximum extent.</param>
    public static implicit operator ScrollBoxSizingStrategy(double maxExtent) => Max(maxExtent);

    /// <summary>
    /// Converts a string representation — <c>"Fill"</c>, <c>"Unbounded"</c> or a number (which
    /// becomes <see cref="Max(double)" />) — to a <see cref="ScrollBoxSizingStrategy" />.
    /// </summary>
    /// <param name="inputString">The string representation.</param>
    /// <exception cref="FormatException">The string is not a known mode nor a valid number.</exception>
    public static implicit operator ScrollBoxSizingStrategy(string? inputString)
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

        throw new FormatException($"'{inputString}' is not a valid {nameof(ScrollBoxSizingStrategy)}: expected '{nameof(Fill)}', '{nameof(Unbounded)}' or a positive number.");
    }

    /// <inheritdoc />
    public override string ToString()
        => Mode switch
        {
            ScrollBoxSizingMode.Max => MaxExtent.ToString(CultureInfo.InvariantCulture),
            ScrollBoxSizingMode.Unbounded => nameof(Unbounded),
            _ => nameof(Fill)
        };
}

/// <summary>
/// Type converter for <see cref="ScrollBoxSizingStrategy" />.
/// </summary>
public class ScrollBoxSizingStrategyTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string stringValue ? (ScrollBoxSizingStrategy) stringValue : null;
}
