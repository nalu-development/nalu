namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Mimics an app that clears <see cref="FileSystem.CacheDirectory" /> (= Library/Caches on iOS) at
/// startup — e.g. a "clear cache" routine or an upgrade migration. Armed from the lifecycle page, it
/// runs at the very top of the NEXT launch, before the background session exists, and therefore also
/// deletes downloads nsurlsessiond finished while the app was dead
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
        var cacheDirectory = FileSystem.CacheDirectory;
        var downloads = Directory.Exists(cacheDirectory)
            ? Directory.EnumerateFiles(cacheDirectory, "CFNetworkDownload_*", SearchOption.AllDirectories).Count()
            : 0;
        var deleted = 0;

        foreach (var entry in Directory.EnumerateFileSystemEntries(cacheDirectory))
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

                deleted++;
            }
            catch (Exception)
            {
                // Keep going: a real app's wipe would too.
            }
        }

        Preferences.Default.Set(ResultKey, FormattableString.Invariant($"cacheWipe=deleted {deleted} entries incl. {downloads} CFNetworkDownload files"));
    }
}
