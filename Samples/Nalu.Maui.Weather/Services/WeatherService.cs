namespace Nalu.Maui.Weather.Services;

using Models;
using OpenMeteo;

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherModel>> GetWeatherAsync(float latitude, float longitude, DateTime start, DateTime end);
    Task<IReadOnlyList<AirQualityModel>> GetAirQualityAsync(float latitude, float longitude, DateTime start, DateTime end);
}

public class WeatherService(OpenMeteoClient openMeteo) : IWeatherService
{
    public async Task<IReadOnlyList<AirQualityModel>> GetAirQualityAsync(float latitude, float longitude, DateTime start, DateTime end)
    {
        var startDate = start.Date.ToString("yyyy-MM-dd");
        var endDate = end.Date.ToString("yyyy-MM-dd");

        var iq = await openMeteo.QueryAsync(new AirQualityOptions(latitude, longitude)
        {
            Hourly = new AirQualityOptions.HourlyOptions([
                AirQualityOptions.HourlyOptionsParameter.carbon_monoxide,
                AirQualityOptions.HourlyOptionsParameter.pm10,
                AirQualityOptions.HourlyOptionsParameter.pm2_5,
                AirQualityOptions.HourlyOptionsParameter.ozone,
            ]),
            Start_date = startDate,
            End_date = endDate,
            Timezone = TimeZoneInfo.Local.Id
        }) ?? throw new InvalidOperationException("Cannot retrieve air quality data");

        var times = iq.Hourly!.Time!;

        return times.Select((t, i) => new AirQualityModel
            {
                Time = DateTime.SpecifyKind(DateTime.Parse(t), DateTimeKind.Local),
                Pm25 = iq.Hourly.Pm2_5![i],
                Pm10 = iq.Hourly.Pm10![i],
                O3 = iq.Hourly.Ozone![i],
                Co = iq.Hourly.Carbon_monoxide![i]
            })
            .ToList();
    }

    public async Task<IReadOnlyList<WeatherModel>> GetWeatherAsync(float latitude, float longitude, DateTime start, DateTime end)
    {
        var startDate = start.Date.ToString("yyyy-MM-dd");
        var endDate = end.Date.ToString("yyyy-MM-dd");

        var forecast = await openMeteo.QueryAsync(new WeatherForecastOptions(latitude, longitude)
        {
            Start_date = startDate,
            End_date = endDate,
            Timezone = TimeZoneInfo.Local.Id,
            Hourly = new HourlyOptions([
                HourlyOptionsParameter.uv_index,
                HourlyOptionsParameter.relativehumidity_2m,
                HourlyOptionsParameter.apparent_temperature,
                HourlyOptionsParameter.temperature_2m,
                HourlyOptionsParameter.weathercode,
                HourlyOptionsParameter.winddirection_10m,
                HourlyOptionsParameter.windspeed_10m,
            ])
        });

        var times = forecast!.Hourly!.Time!;

        return times.Select((t, i) => new WeatherModel
            {
                Time = DateTime.SpecifyKind(DateTime.Parse(t), DateTimeKind.Utc),
                Temperature = forecast.Hourly.Temperature_2m![i],
                FeelsLike = forecast.Hourly.Apparent_temperature![i],
                Humidity = forecast.Hourly.Relativehumidity_2m![i],
                UvIndex = forecast.Hourly.Uv_index![i],
                WindSpeed = forecast.Hourly.Windspeed_10m![i],
                WindDirection = forecast.Hourly.Winddirection_10m![i],
                WeatherCode = forecast.Hourly.Weathercode![i]
            })
            .ToList();
    }
}
