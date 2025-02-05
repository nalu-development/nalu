namespace Nalu.Maui.Weather.Models;

public class WeatherModel
{
    public required DateTime UtcTime { get; set; }
    public required float? Temperature { get; set; }
    public required float? FeelsLike { get; set; }
    public required float? Humidity { get; set; }
    public required float? UvIndex { get; set; }
    public required float? WindSpeed { get; set; }
    public required float? WindDirection { get; set; }
    public required int? WeatherCode { get; set; }
}
