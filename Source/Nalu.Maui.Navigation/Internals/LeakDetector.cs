using System.Threading.Channels;

namespace Nalu;

#pragma warning disable IDE0290 // Use primary constructor

internal class LeakDetector : IDisposable
{
    private readonly Lock _lock = new();
    private readonly Channel<WeakReference<object>> _channel = Channel.CreateUnbounded<WeakReference<object>>();
    private volatile bool _disposed;
    private Task<Task>? _workerTask;

    private void EnsureStarted()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("LeakDetector", "The LeakDetector has already been disposed and cannot accept new requests.");
        }

        lock (_lock)
        {
            _workerTask ??= Task.Factory
                                .StartNew(
                                    QueuedTaskRunnerAsync,
                                    CancellationToken.None,
                                    TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.DenyChildAttach,
                                    TaskScheduler.Default
                                );
        }

        return;

        async Task QueuedTaskRunnerAsync()
        {
            await foreach (var task in _channel.Reader.ReadAllAsync())
            {
                var isLeaking = true;
                for (var i = 0; i < 10; i++)
                {
                    // If TryGetTarget returns false, the object is GONE.
                    if (!task.TryGetTarget(out _))
                    {
                        isLeaking = false;
                        break;
                    }
    
                    await Task.Delay(1000).ConfigureAwait(false);
    
                    // Comprehensive GC collection
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                    GC.WaitForPendingFinalizers();
                }

                if (isLeaking && task.TryGetTarget(out var leakedObject))
                {
                    // We only reach here if it survived 10 seconds and 10 GCs
                    await LogLeakAsync(leakedObject, false);
                }
            }
        }
    }

    public void Track(object obj)
    {
        EnsureStarted();

        var weakObject = new WeakReference<object>(obj);

        _ = _channel.Writer.WriteAsync(weakObject);
    }

    public void Dispose()
    {
        _disposed = true;
        _channel.Writer.Complete();
    }

    private static async Task LogLeakAsync(object leakedObject, bool resurrected)
    {
#if NET9_0_OR_GREATER
#pragma warning disable CA1826 // Do not use Enumerable methods on indexable collections. Instead use the collection directly.
        var shell = Application.Current?.Windows.FirstOrDefault()?.Page as Shell;
#pragma warning restore CA1826
#else
        var shell = Application.Current?.MainPage as Shell;
#endif

        if (shell?.CurrentPage is { } page)
        {
            var objectName = leakedObject.GetType().Name + (resurrected ? " (resurrected)" : string.Empty);
#if NET10_0_OR_GREATER
            await page.Dispatcher.DispatchAsync(() => _ = page.DisplayAlertAsync("Leak detected", $"{objectName} still alive", "OK"));
#else
            await page.Dispatcher.DispatchAsync(() => _ = page.DisplayAlert("Leak detected", $"{objectName} still alive", "OK"));
#endif
        }
    }
}
