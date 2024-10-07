namespace Nalu;

/// <summary>
/// Provides lifecycle callbacks.
/// </summary>
public sealed partial class LifecycleHandlers
{
#pragma warning disable IDE0051
    private static IEnumerable<T> GetLifecycleHandlers<T>() => IPlatformApplication.Current!.Services.GetServices<T>();
#pragma warning restore IDE0051
}
