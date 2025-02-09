using System.Globalization;

namespace Nalu.Maui.Weather.Converters;

public class WeatherCodeToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int weatherCode && WeatherData.WeatherCodes.TryGetValue(weatherCode, out var weatherInfo))
        {
            return weatherInfo.Image;
        }

        return WeatherData.WeatherCodes[0].Image; // Default to clear sky
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException(); // One-way binding, no need for ConvertBack
}
