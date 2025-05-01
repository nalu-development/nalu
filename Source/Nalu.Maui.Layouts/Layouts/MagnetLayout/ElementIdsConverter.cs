using System.ComponentModel;
using System.Globalization;

namespace Nalu.MagnetLayout;

/// <summary>
/// Gets the identifiers of elements separated by a comma.
/// </summary>
public class ElementIdsConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string identifiers)
        {
            return identifiers.Split(',', StringSplitOptions.TrimEntries);
        }

        return base.ConvertFrom(context, culture, value);
    }
}
