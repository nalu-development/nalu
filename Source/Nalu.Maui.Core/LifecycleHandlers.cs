namespace Nalu;

/// <summary>
/// Provides lifecycle callbacks.
/// </summary>
public sealed partial class LifecycleHandlers
{
    private static IEnumerable<T> GetLifecycleHandlers<T>() => IPlatformApplication.Current!.Services.GetServices<T>();
}
