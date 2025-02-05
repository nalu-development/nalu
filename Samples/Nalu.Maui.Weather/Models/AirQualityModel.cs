namespace Nalu.Maui.Weather.Models;

public class AirQualityModel
{
    public required DateTime UtcTime { get; set; }
    public required float? Pm25 { get; set; }
    public required float? Pm10 { get; set; }
    public required float? O3 { get; set; }
    public required float? Co { get; set; }
}
