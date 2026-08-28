namespace Nalu;

/// <summary>
/// Handle to a running live activity. Write-mostly: content changes only through
/// patch lambdas, which the library runs on a draft clone under the handle's lock.
/// </summary>
public interface ILiveActivity
{
    /// <summary>Stable identifier of this activity (survives process restarts).</summary>
    string Id { get; }

    /// <summary>
    /// The activity kind given at start. Routes app-customized iOS widget UIs and
    /// selects the Android notification channel.
    /// </summary>
    string Kind { get; }

    /// <summary>Lifecycle state.</summary>
    LiveActivityState State { get; }

    /// <summary>
    /// The last applied content snapshot. Read-only by contract: casting to
    /// <see cref="LiveActivityContent"/> and mutating is unsupported.
    /// </summary>
    ILiveActivityContent Content { get; }

    /// <summary>
    /// Applies <paramref name="patch"/> to a draft clone of the current content and pushes
    /// the result. The patch must be synchronous; it runs under the handle's lock on the
    /// freshest state. A patch producing identical content is skipped entirely.
    /// </summary>
    /// <param name="patch">Mutates the draft; only changes made here are applied.</param>
    /// <param name="alert">Draws the user's attention instead of updating silently.</param>
    Task UpdateAsync(Action<LiveActivityContent> patch, LiveActivityAlert? alert = null);

    /// <summary>
    /// Ends the activity, optionally patching the content one final time
    /// (e.g. "Delivered ✓"). After this the handle is <see cref="LiveActivityState.Ended"/>.
    /// </summary>
    /// <param name="finalPatch">Optional final content mutation.</param>
    /// <param name="dismissal">How the activity leaves the screen.</param>
    Task EndAsync(Action<LiveActivityContent>? finalPatch = null, LiveActivityDismissal dismissal = LiveActivityDismissal.Default);
}

/// <summary>
/// Entry point for live activities. Register with
/// <see cref="NaluLiveActivitiesMauiAppBuilderExtensions.UseNaluLiveActivities"/> and resolve from DI.
/// </summary>
public interface ILiveActivityManager
{
    /// <summary>How well live activities are supported here; see <see cref="LiveActivitySupport"/>.</summary>
    LiveActivitySupport Support { get; }

    /// <summary>
    /// Ensures the app may post live activities (Android 13+ notification runtime permission;
    /// on iOS reflects the user-level Live Activities switch). Returns <c>true</c> when allowed.
    /// </summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Starts a live activity of the given <paramref name="kind"/> with the given content.
    /// The content instance is cloned on intake — later mutations of it are inert.
    /// </summary>
    Task<ILiveActivity> StartAsync(string kind, LiveActivityContent content);

    /// <summary>
    /// The currently known activities, including ones rehydrated after a process restart.
    /// </summary>
    IReadOnlyList<ILiveActivity> Activities { get; }
}
