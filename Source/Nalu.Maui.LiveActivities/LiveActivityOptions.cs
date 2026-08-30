namespace Nalu;

/// <summary>
/// Options for <see cref="NaluLiveActivitiesMauiAppBuilderExtensions.UseNaluLiveActivities"/>.
/// </summary>
public sealed class LiveActivityOptions
{
    private readonly Dictionary<string, LiveActivityKindOptions> _kinds = new(StringComparer.Ordinal);

    /// <summary>
    /// Android below 16 renders the same content as a plain ongoing notification
    /// (no status-bar chip) and <see cref="ILiveActivityManager.Support"/> reports
    /// <see cref="LiveActivitySupport.Degraded"/>. Set to <c>true</c> to disable the
    /// fallback entirely (activities become no-ops there).
    /// </summary>
    public bool DisableAndroidFallback { get; set; }

    /// <summary>
    /// Android notification small icon resource id (e.g. <c>Resource.Drawable.notification_icon</c>).
    /// Defaults to the application icon, which Android renders as a plain silhouette —
    /// providing a dedicated monochrome icon is strongly recommended.
    /// </summary>
    public int AndroidSmallIcon { get; set; }

    /// <summary>Per-kind configuration; see <see cref="LiveActivityKindOptions"/>.</summary>
    public IReadOnlyDictionary<string, LiveActivityKindOptions> Kinds => _kinds;

    /// <summary>
    /// Configures an activity kind: <paramref name="displayName"/> becomes the Android
    /// notification channel name shown to the user in system settings.
    /// </summary>
    public LiveActivityOptions AddKind(string kind, string displayName)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        _kinds[kind] = new LiveActivityKindOptions(displayName);
        return this;
    }

    internal string GetKindDisplayName(string kind)
        => _kinds.TryGetValue(kind, out var options) ? options.DisplayName : kind;
}

/// <summary>Configuration of a single activity kind.</summary>
/// <param name="DisplayName">User-visible name (Android notification channel name).</param>
public sealed record LiveActivityKindOptions(string DisplayName);
