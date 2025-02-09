namespace Nalu.Maui.Weather.Converters;

using System;
using System.Globalization;
using Microsoft.Maui.Controls;

public class WeatherCodeToDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int weatherCode && WeatherData.WeatherCodes.TryGetValue(weatherCode, out var weatherInfo))
        {
            return weatherInfo.Description;
        }

        return "Unknown weather condition";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException(); // One-way binding, no need for ConvertBack
}
