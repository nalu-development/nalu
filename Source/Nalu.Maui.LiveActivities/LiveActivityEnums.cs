namespace Nalu;

/// <summary>
/// Describes how well live activities are supported on the current device.
/// </summary>
public enum LiveActivitySupport
{
    /// <summary>
    /// Live activities cannot be shown at all on this device
    /// (iOS &lt; 16.2, Mac Catalyst, Windows, or the user disabled them).
    /// </summary>
    Unavailable,

    /// <summary>
    /// The content is rendered, but without the premium treatment:
    /// Android &lt; 16 shows a plain ongoing notification with no status-bar chip.
    /// </summary>
    Degraded,

    /// <summary>
    /// Full treatment: iOS 16.2+ Live Activity (Lock Screen + Dynamic Island) or
    /// Android 16+ Live Update (promoted ongoing notification with status-bar chip).
    /// </summary>
    Full
}

/// <summary>
/// Lifecycle state of a live activity.
/// </summary>
public enum LiveActivityState
{
    /// <summary>The activity is visible and can be updated.</summary>
    Active,

    /// <summary>The activity content is stale (past <see cref="LiveActivityContent.StaleAt"/>).</summary>
    Stale,

    /// <summary>The activity has ended and can no longer be updated.</summary>
    Ended,

    /// <summary>
    /// The user removed the activity from screen (swiped the Android notification away, or
    /// cleared the iOS Live Activity). Terminal: further updates are silently dropped rather
    /// than re-posting, which both platforms ask apps not to do. Unlike
    /// <see cref="Ended"/>, updating a dismissed handle does not throw — app code can keep
    /// driving its loop unchanged.
    /// </summary>
    Dismissed
}

/// <summary>
/// Controls how a live activity is removed from screen when it ends.
/// </summary>
public enum LiveActivityDismissal
{
    /// <summary>The system decides (iOS keeps the final content on the Lock Screen for a while).</summary>
    Default,

    /// <summary>Remove the activity immediately.</summary>
    Immediate
}
