namespace Nalu;

#pragma warning disable IDE0290 // Use primary constructor

internal class LeakDetector
{
    private readonly List<WeakReference<object>> _weakReferences;

    // ReSharper disable once ConvertToPrimaryConstructor
    public LeakDetector(IEnumerable<object> objects)
    {
        _weakReferences = new List<WeakReference<object>>(10);
        foreach (var o in objects)
        {
            if (o is BindableObject { BindingContext: { } bindingContext })
            {
                _weakReferences.Add(new WeakReference<object>(bindingContext));
            }

            _weakReferences.Add(new WeakReference<object>(o));
        }
    }

    public async Task EnsureCollectedAsync()
    {
        const int maxAttempts = 10;

        var i = 0;
        while (true)
        {
            ++i;
            await Task.Delay(i * 50).ConfigureAwait(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var leakedObjects = GetLeakedObjects().ToList();
            if (leakedObjects.Count > 0)
            {
                if (i == maxAttempts)
                {
                    LogLeak(leakedObjects);
                    return;
                }
            }
            else
            {
                return;
            }
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

    private IEnumerable<object> GetLeakedObjects()
    {
        foreach (var weakReference in _weakReferences)
        {
            if (weakReference.TryGetTarget(out var target))
            {
                yield return target;
            }
        }
    }
}
