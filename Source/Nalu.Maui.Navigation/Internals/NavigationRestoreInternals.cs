using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nalu;

/// <summary>The persisted navigation-state snapshot: invalidation header + current root + restorable pushed frames.</summary>
internal sealed class NavigationRestoreSnapshot
{
    public int SchemaVersion { get; set; }

    /// <summary>App version+build the snapshot was captured by; a mismatch invalidates it.</summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Hash of the restorable route table (ordered root segments + registered page segments):
    /// renames/removals invalidate the snapshot instead of replaying into a renamed world.
    /// </summary>
    public string? RouteHash { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    /// <summary>Segment of the current root at capture time.</summary>
    public string? RootSegment { get; set; }

    /// <summary>The root page's captured entering intent, when it had one.</summary>
    public NavigationRestoreIntentData? RootIntent { get; set; }

    /// <summary>The restorable prefix of the pushed pages, bottom-up.</summary>
    public List<NavigationRestoreFrameData> Frames { get; set; } = [];
}

/// <summary>One restorable pushed page in the snapshot.</summary>
internal sealed class NavigationRestoreFrameData
{
    public string? Segment { get; set; }
    public NavigationRestoreIntentData? Intent { get; set; }
}

/// <summary>
/// A serialized intent: the type's namespace-qualified FULL NAME (deliberately not
/// assembly-qualified — a rename simply fails resolution and truncates fail-open) + payload.
/// </summary>
internal sealed class NavigationRestoreIntentData
{
    public string? TypeId { get; set; }
    public string? Payload { get; set; }
}

/// <summary>Source-generated (trim/AOT-safe) serialization of the snapshot envelope.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(NavigationRestoreSnapshot))]
internal sealed partial class NavigationRestoreJsonContext : JsonSerializerContext;

/// <summary>A validated, deserialized restore destination produced from a snapshot at boot.</summary>
internal sealed class NavigationRestoreBoot
{
    public required string RootSegment { get; init; }
    public required Type RootPageType { get; init; }
    public object? RootIntent { get; init; }
    public required IReadOnlyList<NavigationRestoreFrame> Frames { get; init; }
}

/// <summary>A replayable pushed frame: segment + resolved page type + deserialized intent.</summary>
internal sealed record NavigationRestoreFrame(string Segment, Type PageType, object? Intent);

/// <summary>
/// Default <see cref="IIntentSerializer"/>: System.Text.Json. Uses the app-supplied
/// source-generated context when <see cref="NavigationRestoreOptions.IntentSerializerContext"/>
/// is set (trim/NativeAOT-safe); falls back to reflection otherwise.
/// </summary>
internal sealed class NavigationDefaultIntentSerializer(INavigationConfiguration configuration) : IIntentSerializer
{
    private JsonSerializerContext? Context => (configuration as NavigationConfigurator)?.RestoreOptions?.IntentSerializerContext;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Intent types are referenced by the app intent-aware lifecycle implementations (preserved); trimmed/AOT apps must supply IntentSerializerContext (documented on IIntentSerializer)."
    )]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Same as IL2026: reflection fallback is bypassed when IntentSerializerContext is supplied.")]
    public string Serialize(object intent)
    {
        var type = intent.GetType();

        return Context is { } context
            ? JsonSerializer.Serialize(intent, type, context)
            : JsonSerializer.Serialize(intent, type);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Intent types are referenced by the app intent-aware lifecycle implementations (preserved); trimmed/AOT apps must supply IntentSerializerContext (documented on IIntentSerializer)."
    )]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Same as IL2026: reflection fallback is bypassed when IntentSerializerContext is supplied.")]
    public object Deserialize(Type intentType, string payload)
    {
        var intent = Context is { } context
            ? JsonSerializer.Deserialize(payload, intentType, context)
            : JsonSerializer.Deserialize(payload, intentType);

        return intent
               ?? throw new InvalidOperationException($"Deserializing a {intentType.FullName} intent produced no value.");
    }
}

/// <summary>
/// Default <see cref="INavigationRestoreStore"/>: a JSON file in the app cache directory
/// (restore data has exactly the "safe to delete" semantics the cache promises).
/// </summary>
internal sealed class NavigationRestoreFileStore : INavigationRestoreStore
{
    // Lazy: FileSystem.CacheDirectory throws on non-platform TFMs (unit tests replace the store).
    private static string FilePath => Path.Combine(FileSystem.CacheDirectory, "nalu-navigation-restore.json");

    public string? ReadAndDelete()
    {
        try
        {
            var path = FilePath;

            if (!File.Exists(path))
            {
                return null;
            }

            var payload = File.ReadAllText(path);
            File.Delete(path);

            return payload;
        }
        catch
        {
            // Fail-open: an unreadable snapshot behaves like no snapshot.
            return null;
        }
    }

    public async Task WriteAsync(string snapshot, CancellationToken cancellationToken)
    {
        // Atomic replace: a kill mid-write must never leave a truncated snapshot behind.
        var path = FilePath;
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, snapshot, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
    }
}

/// <summary>Computes the snapshot invalidation hash from everything a replay resolves by name.</summary>
internal static class NavigationRestoreRouteHash
{
    public static string Compute(IEnumerable<string> orderedRootSegments, IEnumerable<string> pageSegments)
    {
        var builder = new StringBuilder();

        // Root segments keep their structural ORDER: hosts may resolve duplicate root page
        // types by position, so reordering roots changes what a segment restores to.
        builder.Append("roots:").AppendJoin(',', orderedRootSegments);
        builder.Append("|pages:").AppendJoin(',', pageSegments.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));

        return Convert.ToHexString(hash);
    }
}
