namespace Nalu.Maui.DailyHelper.Models;

/// <summary>The list bucket a <see cref="TodoItem"/> belongs to.</summary>
public enum TodoBucket
{
    Today,
    Upcoming,
    Done
}

/// <summary>
/// An immutable to-do entry: every mutation flows through <c>TodoStore</c> as a cache update,
/// so all subscribed pipelines (pages, tab badge) react automatically.
/// </summary>
public sealed record TodoItem(Guid Id, string Title, string? Notes, DateOnly? DueDate, bool IsDone, int SortOrder)
{
    /// <summary>The expected effort, picked with the duration-wheel sheet (optional).</summary>
    public TimeSpan? Duration { get; init; }

    public bool HasDuration => Duration is not null;

    public string DurationLabel
        => Duration is not { } duration ? string.Empty
            : duration.Hours > 0
                ? duration.Minutes > 0 ? $"{duration.Hours} h {duration.Minutes} min" : $"{duration.Hours} h"
                : $"{duration.Minutes} min";

    public TodoBucket Bucket
        => IsDone ? TodoBucket.Done
            : DueDate is { } due && due <= DateOnly.FromDateTime(DateTime.Today) ? TodoBucket.Today
            : TodoBucket.Upcoming;

    public bool IsOverdue => !IsDone && DueDate is { } due && due < DateOnly.FromDateTime(DateTime.Today);

    public bool HasDueLabel => DueDate is not null;

    public string DueLabel
        => DueDate is not { } due ? string.Empty
            : due == DateOnly.FromDateTime(DateTime.Today) ? "Today"
            : due == DateOnly.FromDateTime(DateTime.Today).AddDays(1) ? "Tomorrow"
            : due.ToString("ddd d MMM");

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public string CheckGlyph => IsDone ? "\ue86c" /* check_circle */ : "\ue836" /* radio_button_unchecked */;
}
