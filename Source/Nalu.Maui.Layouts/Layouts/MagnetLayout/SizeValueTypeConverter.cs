using System.ComponentModel;
using System.Globalization;

namespace Nalu.MagnetLayout;

/// <summary>
/// Type converter for <see cref="SizeValue"/>.
/// </summary>
public class SizeValueTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string strValue)
        {
            return (SizeValue)strValue;
        }

        return null;
    }
}

