namespace Nalu;

#pragma warning disable IDE0290 // Use primary constructor

internal partial class LeakDetector : IDisposable
{
    private class DisposedObject
    {
        private readonly WeakReference<object> _weakRef;

        public int Checks { get; private set; }

        // ReSharper disable once ConvertToPrimaryConstructor
        public DisposedObject(object obj)
        {
            _weakRef = new WeakReference<object>(obj);
        }

        public bool TryGetTarget(out object? target)
        {
            Checks++;
            return _weakRef.TryGetTarget(out target);
        }
    }

    private readonly List<DisposedObject> _disposedObjects = [];
    private CancellationTokenSource _cts = new();

    public void Track(object obj)
    {
        lock (_disposedObjects)
        {
            _disposedObjects.Add(new DisposedObject(obj));
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _ = EnsureCollectedAsync(_cts.Token);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task EnsureCollectedAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();

        const int maxAttempts = 5;
        while (HasDisposedObjects())
        {
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var leakedObjects = new List<object>();
            lock (_disposedObjects)
            {
                for (var i = 0; i < _disposedObjects.Count; i++)
                {
                    var disposedObject = _disposedObjects[i];
                    if (disposedObject.TryGetTarget(out var leakedObject) && disposedObject.Checks >= maxAttempts)
                    {
                        leakedObjects.Add(leakedObject!);
                        _disposedObjects.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (leakedObjects.Count > 0)
            {
                LogLeak(leakedObjects);
            }
        }
    }

    private bool HasDisposedObjects()
    {
        lock (_disposedObjects)
        {
            return _disposedObjects.Count > 0;
        }
    }

    private static void LogLeak(IReadOnlyCollection<object> leakedObjects)
    {
        var shell = Application.Current?.MainPage as Shell;
        if (shell?.CurrentPage is { } page)
        {
            var verb = leakedObjects.Count > 1 ? "are" : "is";
            var objectNames = string.Join(", ", leakedObjects.Select(o => o.GetType().Name));
            page.Dispatcher.Dispatch(() =>
                _ = page.DisplayAlert("Leak detected", $"{objectNames} {verb} still alive", "OK"));
        }
    }
}
