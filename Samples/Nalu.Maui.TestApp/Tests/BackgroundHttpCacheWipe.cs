namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Mimics iOS purging <see cref="FileSystem.CacheDirectory" /> (= Library/Caches on iOS) — CacheDelete,
/// on low storage or "Offload App" — or an app clearing it. Armed from the lifecycle page, it runs at
/// the very top of the NEXT launch, before the background session exists, and therefore also deletes
/// downloads nsurlsessiond finished while the app was dead
/// (Library/Caches/com.apple.nsurlsessiond/Downloads/&lt;bundle id&gt;/CFNetworkDownload_*.tmp).
/// </summary>
internal static class BackgroundHttpCacheWipe
{
    private const string ArmedKey = "BackgroundHttpWipeCachesOnNextLaunch";
    private const string ResultKey = "BackgroundHttpCacheWipeResult";

    /// <summary>What the last startup wipe deleted — persisted, since the wipe may run in a background launch.</summary>
    public static string LastResult => Preferences.Default.Get(ResultKey, "cacheWipe=none");

    public static void ArmForNextLaunch() => Preferences.Default.Set(ArmedKey, true);

    public static void Reset()
    {
        Preferences.Default.Remove(ArmedKey);
        Preferences.Default.Remove(ResultKey);
    }

    public static void RunIfArmed()
    {
        if (!Preferences.Default.Get(ArmedKey, false))
        {
            return;
        }

        Preferences.Default.Remove(ArmedKey);

        // Runs inside FinishedLaunching: it must never throw, or the app aborts at every launch.
        try
        {
            var cacheDirectory = FileSystem.CacheDirectory;
            var downloads = Directory.EnumerateFiles(cacheDirectory, "CFNetworkDownload_*", SearchOption.AllDirectories).Count();
            var entries = Directory.EnumerateFileSystemEntries(cacheDirectory).ToList();

            // What iOS's CacheDelete does on a low-storage purge or "Offload App" is a one-shot
            // rename of the whole directory, recreated empty — a privilege the sandboxed app does
            // not have. Its own fallback ("clearing in place") is what an app can reproduce:
            // every child goes, the directory stays.
            foreach (var entry in entries)
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
                catch (Exception)
                {
                    // Keep going, like the system's in-place clearing.
                }
            }

            Preferences.Default.Set(ResultKey, FormattableString.Invariant($"cacheWipe=deleted {entries.Count} entries incl. {downloads} CFNetworkDownload files"));
        }
        catch (Exception ex)
        {
            Preferences.Default.Set(ResultKey, $"cacheWipe=failed {ex.GetType().Name}: {ex.Message}");
        }
    }
}
