using System.ComponentModel;
using System.Globalization;

namespace Nalu.MagnetLayout;

/// <summary>
/// Converts a string to a <see cref="HorizontalPullTarget" /> object.
/// </summary>
public class HorizontalPullTargetConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            return (HorizontalPullTarget) str;
        }

        return base.ConvertFrom(context, culture, value);
    }
}
