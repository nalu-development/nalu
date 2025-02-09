namespace Nalu.Maui.Weather.Converters;

public static class WeatherData
{
    public static readonly Dictionary<int, (string Description, string Icon, string Image)> WeatherCodes = new()
                                                                                                           {
                                                                                                               {
                                                                                                                   0,
                                                                                                                   ("Clear sky", "\ue430",
                                                                                                                       "https://images.unsplash.com/photo-1601297183305-6df142704ea2?ixlib=rb-4.0.3&q=75&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=ritam-baishya-ROVBDer29PQ-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   1,
                                                                                                                   ("Mainly clear", "\ue430",
                                                                                                                       "https://images.unsplash.com/photo-1601297183305-6df142704ea2?ixlib=rb-4.0.3&q=75&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=ritam-baishya-ROVBDer29PQ-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   2,
                                                                                                                   ("Partly cloudy", "\ue42d",
                                                                                                                       "https://images.unsplash.com/photo-1601297183305-6df142704ea2?ixlib=rb-4.0.3&q=75&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=ritam-baishya-ROVBDer29PQ-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   3,
                                                                                                                   ("Overcast", "\ue3dd",
                                                                                                                       "https://images.unsplash.com/photo-1499956827185-0d63ee78a910?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=barry-simon-4C6Rp23RjnE-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   45,
                                                                                                                   ("Fog", "\uf029",
                                                                                                                       "https://images.unsplash.com/photo-1512923927402-a9867a68180e?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=chris-lawton-6tfO1M8_gas-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   48,
                                                                                                                   ("Depositing rime fog", "\uf029",
                                                                                                                       "https://images.unsplash.com/photo-1512923927402-a9867a68180e?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=chris-lawton-6tfO1M8_gas-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   51,
                                                                                                                   ("Drizzle: Light intensity", "\ue3ea",
                                                                                                                       "https://images.unsplash.com/photo-1556485689-33e55ab56127?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=roman-synkevych-qPvBmSvmohs-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   53,
                                                                                                                   ("Drizzle: Moderate intensity", "\ue3ea",
                                                                                                                       "https://images.unsplash.com/photo-1556485689-33e55ab56127?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=roman-synkevych-qPvBmSvmohs-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   55,
                                                                                                                   ("Drizzle: Dense intensity", "\ue3ea",
                                                                                                                       "https://images.unsplash.com/photo-1556485689-33e55ab56127?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=roman-synkevych-qPvBmSvmohs-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   56,
                                                                                                                   ("Freezing Drizzle: Light intensity", "\ue798",
                                                                                                                       "https://images.unsplash.com/photo-1505404919723-002ecad81b92?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=philippe-tarbouriech-rWwj4zcOcIs-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   57,
                                                                                                                   ("Freezing Drizzle: Dense intensity", "\ue798",
                                                                                                                       "https://images.unsplash.com/photo-1505404919723-002ecad81b92?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=philippe-tarbouriech-rWwj4zcOcIs-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   61,
                                                                                                                   ("Rain: Slight intensity", "\uf1ad",
                                                                                                                       "https://images.unsplash.com/photo-1519692933481-e162a57d6721?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=osman-rana-GXEZuWo5m4I-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   63,
                                                                                                                   ("Rain: Moderate intensity", "\uf1ad",
                                                                                                                       "https://images.unsplash.com/photo-1519692933481-e162a57d6721?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=osman-rana-GXEZuWo5m4I-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   65,
                                                                                                                   ("Rain: Heavy intensity", "\uf1ad",
                                                                                                                       "https://images.unsplash.com/photo-1519692933481-e162a57d6721?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=osman-rana-GXEZuWo5m4I-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   66,
                                                                                                                   ("Freezing Rain: Light intensity", "\ueb3b",
                                                                                                                       "https://images.unsplash.com/photo-1645221986876-8e3255ba006c?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=benjamin-lehman-jHpqNFSOSA0-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   67,
                                                                                                                   ("Freezing Rain: Heavy intensity", "\ueb3b",
                                                                                                                       "https://images.unsplash.com/photo-1645221986876-8e3255ba006c?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=benjamin-lehman-jHpqNFSOSA0-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   71,
                                                                                                                   ("Snow fall: Slight intensity", "\uebd3",
                                                                                                                       "https://images.unsplash.com/photo-1627854879776-c6eab3d4b7a6?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=martina-de-marchena-HJ2Ayo5yOFE-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   73,
                                                                                                                   ("Snow fall: Moderate intensity", "\uebd3",
                                                                                                                       "https://images.unsplash.com/photo-1627854879776-c6eab3d4b7a6?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=martina-de-marchena-HJ2Ayo5yOFE-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   75,
                                                                                                                   ("Snow fall: Heavy intensity", "\uebd3",
                                                                                                                       "https://images.unsplash.com/photo-1627854879776-c6eab3d4b7a6?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=martina-de-marchena-HJ2Ayo5yOFE-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   77,
                                                                                                                   ("Snow grains", "\uebd3",
                                                                                                                       "https://images.unsplash.com/photo-1627854879776-c6eab3d4b7a6?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=martina-de-marchena-HJ2Ayo5yOFE-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   80,
                                                                                                                   ("Rain showers: Slight", "\uf061",
                                                                                                                       "https://images.unsplash.com/photo-1496034663057-6245f11be793?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=liv-bruce-8yt8kBuEqok-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   81,
                                                                                                                   ("Rain showers: Moderate", "\uf061",
                                                                                                                       "https://images.unsplash.com/photo-1496034663057-6245f11be793?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=liv-bruce-8yt8kBuEqok-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   82,
                                                                                                                   ("Rain showers: Violent", "\uebdb",
                                                                                                                       "https://images.unsplash.com/photo-1496034663057-6245f11be793?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=liv-bruce-8yt8kBuEqok-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   85,
                                                                                                                   ("Snow showers: Slight", "\uebd3",
                                                                                                                       "https://images.unsplash.com/photo-1496034663057-6245f11be793?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=liv-bruce-8yt8kBuEqok-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   86,
                                                                                                                   ("Snow showers: Heavy", "\uebd3",
                                                                                                                       "https://images.unsplash.com/photo-1496034663057-6245f11be793?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=liv-bruce-8yt8kBuEqok-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   95,
                                                                                                                   ("Thunderstorm: Slight or moderate", "\uebdb",
                                                                                                                       "https://images.unsplash.com/photo-1605727216801-e27ce1d0cc28?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=tasos-mansour-_hGPdpyMV-8-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   96,
                                                                                                                   ("Thunderstorm with slight hail", "\uebdb",
                                                                                                                       "https://images.unsplash.com/photo-1605727216801-e27ce1d0cc28?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=tasos-mansour-_hGPdpyMV-8-unsplash.jpg")
                                                                                                               },
                                                                                                               {
                                                                                                                   99,
                                                                                                                   ("Thunderstorm with heavy hail", "\uebdb",
                                                                                                                       "https://images.unsplash.com/photo-1605727216801-e27ce1d0cc28?ixlib=rb-4.0.3&q=80&fm=jpg&w=1200&auto=format&fit=crop&cs=srgb&dl=tasos-mansour-_hGPdpyMV-8-unsplash.jpg")
                                                                                                               }
                                                                                                           };
}
