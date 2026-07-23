namespace Nalu.Maui.TestApp;

/// <summary>
/// Tracks weak references to objects that are EXPECTED to be garbage-collected
/// (e.g. page models disposed by Nalu navigation) so UI tests can assert they don't leak.
/// MainPage exposes CheckLeaksButton / LeaksLabel on top of this.
/// </summary>
/// <remarks>
/// Objects that die with the whole shell teardown are excluded via <see cref="Draining"/>:
/// on MAUI 10 iOS a Shell instance swapped out of the window is retained by the platform
/// even when vanilla (see the "Plain Shell" test page — a plain ContentPage collects,
/// a plain Shell does not), so only pages disposed DURING navigation are assertable.
/// </remarks>
public static class LeakTracker
{
    private static readonly Lock _lock = new();
    private static readonly List<(string Name, WeakReference Reference)> _expectCollected = [];

    /// <summary>True while the app-level reset is tearing down the current shell.</summary>
    public static bool Draining { get; set; }

    public static void ExpectCollected(object instance)
    {
        if (Draining)
        {
            return;
        }

        lock (_lock)
        {
            _expectCollected.Add((instance.GetType().Name, new WeakReference(instance)));
        }
    }

    /// <summary>Drops surviving entries (used after asserting a KNOWN platform leak).</summary>
    public static void Forgive()
    {
        lock (_lock)
        {
            _expectCollected.Clear();
        }
    }

    /// <summary>Forces full collections and reports survivors as "Leaked:N name1,name2".</summary>
    public static async Task<string> CheckAsync()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

            lock (_lock)
            {
                if (_expectCollected.TrueForAll(t => !t.Reference.IsAlive))
                {
                    break;
                }
            }

            await Task.Delay(150).ConfigureAwait(false);
        }

        lock (_lock)
        {
            var alive = _expectCollected.Where(t => t.Reference.IsAlive).Select(t => t.Name).ToList();
            _expectCollected.RemoveAll(t => !t.Reference.IsAlive);

            return alive.Count == 0 ? "Leaked:0" : $"Leaked:{alive.Count} {string.Join(",", alive.Distinct())}";
        }
    }
}
