namespace Nalu.Maui.Weather.Models;

public class HourlyWeatherModel
{
    public required DateTime Time { get; set; }
    public required float? Temperature { get; set; }
    public required float? FeelsLike { get; set; }
    public required float? Humidity { get; set; }
    public required float? UvIndex { get; set; }
    public required float? WindSpeed { get; set; }
    public required float? WindDirection { get; set; }
    public required int? WeatherCode { get; set; }
    public string Hour => Time.ToString("HH:mm");
    public string TemperatureDegrees => $"{Temperature ?? 0:N0}°";
}
