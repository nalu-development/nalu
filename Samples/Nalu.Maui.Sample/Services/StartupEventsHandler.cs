using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Nalu.Maui.Sample.Services;

[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates")]
public class StartupEventsHandler(ILogger<StartupEventsHandler> logger)
#if IOS
    : INSUrlBackgroundSessionLostMessageHandler
#endif
{
    public ObservableCollection<string> BackgroundResponses { get; } = [];

#if IOS
    async Task INSUrlBackgroundSessionLostMessageHandler.HandleLostMessageAsync(NSUrlBackgroundResponseHandle responseHandle)
    {
        try
        {
            logger.LogDebug("Getting background response for {RequestKey}", responseHandle.RequestIdentifier);
            using var response = await responseHandle.GetResponseAsync();
            logger.LogDebug("Reading background response for {RequestKey}", responseHandle.RequestIdentifier);
            var content = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Got background {StatusCode} response for {RequestKey}: {Content}", response.StatusCode, responseHandle.RequestIdentifier, content);
            BackgroundResponses.Add($"{responseHandle.RequestIdentifier} => [{response.StatusCode}]: {content}");
            logger.LogDebug("Added background response for {RequestKey}", responseHandle.RequestIdentifier);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling background response {RequestKey} {ErrorMessage}", responseHandle.RequestIdentifier, ex.Message);
        }
    }
#endif
}
