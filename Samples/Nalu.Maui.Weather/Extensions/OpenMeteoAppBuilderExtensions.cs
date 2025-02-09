using System.Net.Http.Headers;
using System.Reflection;
using OpenMeteo;

namespace Nalu.Maui.Weather;

public static class OpenMeteoAppBuilderExtensions
{
    public static MauiAppBuilder UseOpenMeteo(this MauiAppBuilder builder)
    {
#if IOS
        HttpClient httpClient = DeviceInfo.DeviceType == DeviceType.Virtual
            ? new()
            : new(new NSUrlBackgroundSessionHttpMessageHandler());
#else
        HttpClient httpClient = new();
#endif
        httpClient.DefaultRequestHeaders.Accept.Clear();

        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("om-dotnet");

        // We have to use reflection to set our own HttpClient, sad, isn't it?
        // https://github.com/AlienDwarf/open-meteo-dotnet/blob/master/OpenMeteo/HttpController.cs
        var openMeteoClient = new OpenMeteoClient();

        var controller = openMeteoClient
                         .GetType()
                         .GetField("httpController", BindingFlags.Instance | BindingFlags.NonPublic)!
                         .GetValue(openMeteoClient)!;

        controller
            .GetType()
            .GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(controller, httpClient);

        builder.Services.AddSingleton(openMeteoClient);

        return builder;
    }
}
