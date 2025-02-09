using System.Globalization;

namespace Nalu.Maui.Weather.Converters;

public class WeatherCodeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int weatherCode && WeatherData.WeatherCodes.TryGetValue(weatherCode, out var weatherInfo))
        {
            return weatherInfo.Icon;
        }

        return "help"; // Default icon if code is unknown
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException(); // One-way binding, no need for ConvertBack
}
