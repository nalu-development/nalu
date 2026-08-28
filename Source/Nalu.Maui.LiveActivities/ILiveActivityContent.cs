namespace Nalu;

/// <summary>
/// Read-only view of a live activity's content, exposed by <see cref="ILiveActivity.Content"/>.
/// </summary>
/// <remarks>
/// This is the last applied snapshot: it does not reflect a patch still running inside
/// <see cref="ILiveActivity.UpdateAsync"/>. Treat it as immutable — casting it back to
/// <see cref="LiveActivityContent"/> and mutating it is unsupported.
/// </remarks>
public interface ILiveActivityContent
{
    /// <inheritdoc cref="LiveActivityContent.Title"/>
    string? Title { get; }

    /// <inheritdoc cref="LiveActivityContent.Subtitle"/>
    string? Subtitle { get; }

    /// <inheritdoc cref="LiveActivityContent.ChipText"/>
    string? ChipText { get; }

    /// <inheritdoc cref="LiveActivityContent.ChipIcon"/>
    string? ChipIcon { get; }

    /// <inheritdoc cref="LiveActivityContent.AccentColor"/>
    string? AccentColor { get; }

    /// <inheritdoc cref="LiveActivityContent.ImageName"/>
    string? ImageName { get; }

    /// <inheritdoc cref="LiveActivityContent.Progress"/>
    ILiveActivityProgress? Progress { get; }

    /// <inheritdoc cref="LiveActivityContent.Timer"/>
    ILiveActivityTimer? Timer { get; }

    /// <inheritdoc cref="LiveActivityContent.DeepLink"/>
    string? DeepLink { get; }

    /// <inheritdoc cref="LiveActivityContent.StaleAt"/>
    DateTimeOffset? StaleAt { get; }

    /// <inheritdoc cref="LiveActivityContent.Actions"/>
    IReadOnlyList<ILiveActivityAction>? Actions { get; }

    /// <inheritdoc cref="LiveActivityContent.Custom"/>
    IReadOnlyDictionary<string, string>? Custom { get; }
}

/// <summary>
/// Read-only view of <see cref="LiveActivityProgress"/>.
/// </summary>
public interface ILiveActivityProgress
{
    /// <inheritdoc cref="LiveActivityProgress.Value"/>
    double? Value { get; }

    /// <inheritdoc cref="LiveActivityProgress.Indeterminate"/>
    bool Indeterminate { get; }

    /// <inheritdoc cref="LiveActivityProgress.TrackerIcon"/>
    string? TrackerIcon { get; }

    /// <inheritdoc cref="LiveActivityProgress.Segments"/>
    IReadOnlyList<ILiveActivityProgressSegment>? Segments { get; }

    /// <inheritdoc cref="LiveActivityProgress.Points"/>
    IReadOnlyList<ILiveActivityProgressPoint>? Points { get; }
}

/// <summary>
/// Read-only view of <see cref="LiveActivityProgressSegment"/>.
/// </summary>
public interface ILiveActivityProgressSegment
{
    /// <inheritdoc cref="LiveActivityProgressSegment.Weight"/>
    double Weight { get; }

    /// <inheritdoc cref="LiveActivityProgressSegment.Color"/>
    string? Color { get; }
}

/// <summary>
/// Read-only view of <see cref="LiveActivityProgressPoint"/>.
/// </summary>
public interface ILiveActivityProgressPoint
{
    /// <inheritdoc cref="LiveActivityProgressPoint.Position"/>
    double Position { get; }

    /// <inheritdoc cref="LiveActivityProgressPoint.Icon"/>
    string? Icon { get; }
}

/// <summary>
/// Read-only view of <see cref="LiveActivityTimer"/>.
/// </summary>
public interface ILiveActivityTimer
{
    /// <inheritdoc cref="LiveActivityTimer.Mode"/>
    LiveActivityTimerMode Mode { get; }

    /// <inheritdoc cref="LiveActivityTimer.StartsAt"/>
    DateTimeOffset? StartsAt { get; }

    /// <inheritdoc cref="LiveActivityTimer.EndsAt"/>
    DateTimeOffset? EndsAt { get; }

    /// <inheritdoc cref="LiveActivityTimer.PausedElapsed"/>
    TimeSpan? PausedElapsed { get; }
}

/// <summary>
/// Read-only view of <see cref="LiveActivityAction"/>.
/// </summary>
public interface ILiveActivityAction
{
    /// <inheritdoc cref="LiveActivityAction.Label"/>
    string Label { get; }

    /// <inheritdoc cref="LiveActivityAction.Icon"/>
    string? Icon { get; }

    /// <inheritdoc cref="LiveActivityAction.DeepLink"/>
    string DeepLink { get; }
}
