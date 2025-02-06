namespace Nalu.Maui.Weather.Converters;

using System.Collections.Generic;

public static class WeatherData
{
    public static readonly Dictionary<int, (string Description, string Icon)> WeatherCodes = new()
    {
        { 0, ("Clear sky", "\ue430") },
        { 1, ("Mainly clear", "\ue430") },
        { 2, ("Partly cloudy", "\ue42d") },
        { 3, ("Overcast", "\ue3dd") },
        { 45, ("Fog", "\uf029") },
        { 48, ("Depositing rime fog", "\uf029") },
        { 51, ("Drizzle: Light intensity", "\ue3ea") },
        { 53, ("Drizzle: Moderate intensity", "\ue3ea") },
        { 55, ("Drizzle: Dense intensity", "\ue3ea") },
        { 56, ("Freezing Drizzle: Light intensity", "\ue798") },
        { 57, ("Freezing Drizzle: Dense intensity", "\ue798") },
        { 61, ("Rain: Slight intensity", "\uf1ad") },
        { 63, ("Rain: Moderate intensity", "\uf1ad") },
        { 65, ("Rain: Heavy intensity", "\uf1ad") },
        { 66, ("Freezing Rain: Light intensity", "\ueb3b") },
        { 67, ("Freezing Rain: Heavy intensity", "\ueb3b") },
        { 71, ("Snow fall: Slight intensity", "\uebd3") },
        { 73, ("Snow fall: Moderate intensity", "\uebd3") },
        { 75, ("Snow fall: Heavy intensity", "\uebd3") },
        { 77, ("Snow grains", "\uebd3") },
        { 80, ("Rain showers: Slight", "\uf061") },
        { 81, ("Rain showers: Moderate", "\uf061") },
        { 82, ("Rain showers: Violent", "\uebdb") },
        { 85, ("Snow showers: Slight", "\uebd3") },
        { 86, ("Snow showers: Heavy", "\uebd3") },
        { 95, ("Thunderstorm: Slight or moderate", "\uebdb") },
        { 96, ("Thunderstorm with slight hail", "\uebdb") },
        { 99, ("Thunderstorm with heavy hail", "\uebdb") }
    };
}

