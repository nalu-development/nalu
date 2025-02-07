namespace Nalu.Maui.Weather.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using Resources;

public class HourlyAirQualityModel
{
    public required DateTime Time { get; set; }
    public required float? Pm25 { get; set; }
    public required float? Pm10 { get; set; }
    public required float? O3 { get; set; }
    public required float? Co { get; set; }

    public string Icon
    {
        get
        {
            if (Pm25 is > 50 || Pm10 is > 50 || Co is > 1000)
            {
                return "\ue99a";
            }

            if (Pm25 is > 25 || Pm10 is > 25 || Co is > 600)
            {
                return "\uf083";
            }

            return "\ue1d5";
        }
    }

    public Color IconColor
    {
        get
        {
            if (Pm25 is > 50 || Pm10 is > 50 || Co is > 1000)
            {
                return Colors.Red;
            }

            if (Pm25 is > 25 || Pm10 is > 25 || Co is > 600)
            {
                return Colors.Orange;
            }

            return Colors.Green;
        }
    }

    public string DangerousUnit
    {
        get
        {
            var pm25Danger = Pm25.HasValue ? Pm25.Value / 50 : 0;
            var pm10Danger = Pm10.HasValue ? Pm10.Value / 50 : 0;
            var o3Danger = O3.HasValue ? O3.Value / 180 : 0;
            var coDanger = Co.HasValue ? Co.Value / 1000 : 0;

            var maxDanger = new[] { pm25Danger, pm10Danger, o3Danger, coDanger }.Max();

            if (maxDanger == pm25Danger)
            {
                return Texts.PM25;
            }

            if (maxDanger == pm10Danger)
            {
                return Texts.PM10;
            }

            return maxDanger == o3Danger ? "O3" : "CO";
        }
    }

    public string DangerousValue
    {
        get
        {
            var dangerousUnit = DangerousUnit;
            if (dangerousUnit == Texts.PM25)
            {
                return Pm25Value;
            }

            if (dangerousUnit == Texts.PM10)
            {
                return Pm10Value;
            }

            if (dangerousUnit == "O3")
            {
                return O3Value;
            }

            return CoValue;
        }
    }

    public string DangerousLevel
    {
        get
        {
            if (Pm25 is > 50 || Pm10 is > 50 || Co is > 1000)
            {
                return "Dangerous";
            }

            if (Pm25 is > 25 || Pm10 is > 25 || Co is > 600)
            {
                return "Unhealthy";
            }

            return "Good";
        }
    }

    public string Hour => Time.ToString("HH:mm");
    public string Pm25Value => $"{Pm25 ?? 0:N0} μg/m³";
    public string Pm10Value => $"{Pm10 ?? 0:N0} μg/m³";
    public string O3Value => $"{O3 ?? 0:N0} μg/m³";
    public string CoValue => $"{Co ?? 0:N1} mg/m³";
}
