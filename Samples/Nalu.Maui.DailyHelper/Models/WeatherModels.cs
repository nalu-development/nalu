namespace Nalu.Maui.DailyHelper.Models;

/// <summary>One hour of forecast, already converted to the display unit.</summary>
public sealed record HourForecast(
    DateTime Time,
    double Temperature,
    double FeelsLike,
    double Precipitation,
    double WindSpeed,
    double Humidity,
    int WeatherCode)
{
    public string TimeLabel => Time.ToString("HH:mm");
    public string TemperatureLabel => $"{Temperature:0}°";
    public string PrecipitationLabel => $"{Precipitation:0.#} mm";
    public string WindLabel => $"{WindSpeed:0} km/h";
    public string HumidityLabel => $"{Humidity:0}%";
    public string Glyph => WeatherInfo.GlyphFor(WeatherCode, Time.Hour is < 7 or > 21);
}

/// <summary>The "right now" snapshot rendered by the Today hero and the weather detail page.</summary>
public sealed record CurrentConditions(
    string Place,
    DateTime Time,
    double Temperature,
    double FeelsLike,
    double TemperatureMin,
    double TemperatureMax,
    double Precipitation,
    double WindSpeed,
    double Humidity,
    int WeatherCode)
{
    public string TemperatureLabel => $"{Temperature:0}°";
    public string FeelsLikeLabel => $"{FeelsLike:0}°";
    public string MinMaxLabel => $"H {TemperatureMax:0}°  L {TemperatureMin:0}°";
    public string PrecipitationLabel => $"{Precipitation:0.#} mm";
    public string WindLabel => $"{WindSpeed:0} km/h";
    public string HumidityLabel => $"{Humidity:0}%";
    public string Description => WeatherInfo.DescriptionFor(WeatherCode);
    public string Glyph => WeatherInfo.GlyphFor(WeatherCode, Time.Hour is < 7 or > 21);
    public string Photo => WeatherInfo.PhotoFor(WeatherCode);
}

/// <summary>A per-day roll-up of the hourly forecast (weather detail "Coming days" rows).</summary>
public sealed record DaySummary(DateTime Date, int WeatherCode, double TemperatureMin, double TemperatureMax)
{
    public string DayLabel => Date.ToString("dddd d MMMM");
    public string Glyph => WeatherInfo.GlyphFor(WeatherCode, night: false);
    public string MinMaxLabel => $"{TemperatureMin:0}° / {TemperatureMax:0}°";
}

/// <summary>Maps WMO weather codes to descriptions, Material glyphs and a mood photo.</summary>
public static class WeatherInfo
{
    public static string DescriptionFor(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle",
        >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 or 96 or 99 => "Thunderstorm",
        _ => "Unknown"
    };

    public static string GlyphFor(int code, bool night = false) => code switch
    {
        0 or 1 => night ? "\uea46" /* nights_stay */ : "\ue81a" /* sunny */,
        2 => "\ue42d" /* wb_cloudy */,
        3 => "\ue2bd" /* cloud */,
        45 or 48 => "\ue818" /* foggy */,
        >= 51 and <= 57 => "\ue3ea" /* grain */,
        (>= 61 and <= 67) or 80 or 81 or 82 => "\ue52f" /* umbrella */,
        (>= 71 and <= 77) or 85 or 86 => "\ueb3b" /* ac_unit */,
        95 or 96 or 99 => "\uf070" /* storm */,
        _ => "\ue2bd"
    };

    public static string PhotoFor(int code) => code switch
    {
        0 or 1 or 2 => "https://images.unsplash.com/photo-1601297183305-6df142704ea2?q=75&fm=jpg&w=1200&fit=crop",
        3 => "https://images.unsplash.com/photo-1499956827185-0d63ee78a910?q=75&fm=jpg&w=1200&fit=crop",
        45 or 48 => "https://images.unsplash.com/photo-1512923927402-a9867a68180e?q=75&fm=jpg&w=1200&fit=crop",
        (>= 71 and <= 77) or 85 or 86 => "https://images.unsplash.com/photo-1478265409131-1f65c88f965c?q=75&fm=jpg&w=1200&fit=crop",
        95 or 96 or 99 => "https://images.unsplash.com/photo-1605727216801-e27ce1d0cc28?q=75&fm=jpg&w=1200&fit=crop",
        _ => "https://images.unsplash.com/photo-1519692933481-e162a57d6721?q=75&fm=jpg&w=1200&fit=crop"
    };
}
