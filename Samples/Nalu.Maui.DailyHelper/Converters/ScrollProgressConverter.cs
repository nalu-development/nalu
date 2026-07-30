using System.Globalization;

namespace Nalu.Maui.DailyHelper.Converters;

/// <summary>
/// Maps a scroll offset (e.g. <see cref="ScaffoldNavBarContext.ScrollOffset"/> via
/// <c>NavBarBinding</c>) to a 0→1 progress over the [<see cref="Start"/>,
/// <see cref="Start"/> + <see cref="Length"/>] range — the building block of scroll-driven
/// chrome (title fades, bar materialization).
/// </summary>
public sealed class ScrollProgressConverter : IValueConverter
{
    /// <summary>Gets or sets the offset at which progress leaves 0.</summary>
    public double Start { get; set; }

    /// <summary>Gets or sets the offset distance over which progress reaches 1.</summary>
    public double Length { get; set; } = 1;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double offset ? Math.Clamp((offset - Start) / Length, 0d, 1d) : 0d;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
