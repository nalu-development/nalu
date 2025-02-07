namespace Nalu.Maui.Weather.Models;

public class DailyWeatherModel
{
    public DateTime Time { get; set; }
    public float WindSpeed { get; set; }
    public float WindDirection { get; set; }
    public float TemperatureMin { get; set; }
    public float TemperatureMax { get; set; }
    public float RainSum { get; set; }
    public int WeatherCode { get; set; }
    public string DayName => Time.ToString("dddd");
    public string Date => Time.ToString("M");
    public string TemperatureMinDegrees => $"{TemperatureMin:N0}°";
    public string TemperatureMaxDegrees => $"{TemperatureMax:N0}°";
    public string RainSumMm => $"{RainSum:N1}mm";
    public string WindSpeedKmh => $"{WindSpeed:N0}km/h {WindDirection}°";
}
