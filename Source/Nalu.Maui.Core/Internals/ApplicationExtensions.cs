using Microsoft.Extensions.Logging;

namespace Nalu.Internals;

internal static class ApplicationExtensions
{
    public static ILogger? GetLogger<T>(this Application? application)
        where T : class => application?.Windows.FirstOrDefault()
                                      ?.Page?.Handler?.MauiContext?.Services
                                      .GetService<ILogger<T>>();
}
