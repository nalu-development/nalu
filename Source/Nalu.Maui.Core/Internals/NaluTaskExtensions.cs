using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Nalu.Internals;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Proven safe in MAUI")]
[SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Proven safe in MAUI")]
internal static class NaluTaskExtensions
{
    public static async void FireAndForget<TResult>(this Task<TResult> task, Action<Exception>? errorCallback = null)
    {
        TResult? result = default;

        try
        {
            result = await task.ConfigureAwait(false);
        }
        catch (Exception exc)
        {
            errorCallback?.Invoke(exc);
#if DEBUG
            throw;
#endif
        }
    }

    public static async void FireAndForget(this Task task, Action<Exception>? errorCallback = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errorCallback?.Invoke(ex);
#if DEBUG
            throw;
#endif
        }
    }

    public static void FireAndForget(this Task task, ILogger? logger, [CallerMemberName] string? callerName = null) =>
        task.FireAndForget(ex => Log(logger, ex, callerName));

    public static void FireAndForget<T>(this Task task, T? viewHandler, [CallerMemberName] string? callerName = null)
        where T : IElementHandler =>
        task.FireAndForget(ex => Log(viewHandler?.CreateLogger<T>(), ex, callerName));

    private static ILogger? CreateLogger<T>(this IElementHandler? elementHandler) =>
        elementHandler?.MauiContext?.Services?.GetService<ILogger<T>>();

#pragma warning disable CA1848
    private static void Log(ILogger? logger, Exception ex, string? callerName) =>
        logger?.LogError(ex, "Unexpected exception in {Member}.", callerName);
#pragma warning restore CA1848
}
