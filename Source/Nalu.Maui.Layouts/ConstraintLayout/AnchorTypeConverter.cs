namespace Nalu;

using System.ComponentModel;
using System.Globalization;

/// <summary>
/// Converts an <see cref="Anchor"/> to and from a string.
/// </summary>
public class AnchorTypeConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    /// <inheritdoc/>
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value switch
        {
            string s => (Anchor)s,
            _ => throw new NotSupportedException(),
        };

    /// <inheritdoc/>
    public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value switch
        {
            Anchor anchor => anchor.ToString(),
            _ => throw new NotSupportedException(),
        };
}
