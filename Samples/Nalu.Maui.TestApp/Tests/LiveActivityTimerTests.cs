using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Timer-centric live activity harness: an appointment whose activity counts DOWN to the
/// appointment end while it is running, then flips to counting UP the overflow once the
/// end passes. The OS renders the ticking natively in both phases (iOS
/// <c>Text(timerInterval:)</c>/<c>.timer</c>, Android chronometer); the app only sends
/// ONE update at the boundary — reaching zero never ends or updates anything by itself,
/// flipping to overflow is an explicit state change owned by the app.
/// </summary>
[UsedImplicitly]
[TestPage("Live Activity Timer Tests")]
public class LiveActivityTimerTests : ContentPage
{
    private static readonly TimeSpan _appointmentDuration = TimeSpan.FromMinutes(2);

    private readonly ILiveActivityManager _manager;
    private readonly Label _statusLabel;
    private ILiveActivity? _activity;
    private DateTimeOffset _appointmentEnd;
    private CancellationTokenSource? _overflowWatch;

    public LiveActivityTimerTests(ILiveActivityManager manager)
    {
        _manager = manager;

        // Reconcile with an appointment that survived a process restart: adopt it and
        // re-arm the overflow watcher instead of starting a duplicate next to it.
        _activity = _manager.Activities.LastOrDefault(a => a.Kind == "appointment" && a.State != LiveActivityState.Ended);

        var status = $"Support: {_manager.Support}";

        if (_activity?.Content.Timer is { } timer)
        {
            if (timer is { Mode: LiveActivityTimerMode.CountDown, EndsAt: { } endsAt })
            {
                _appointmentEnd = endsAt;
                status = $"Adopted appointment until {endsAt:HH:mm:ss} ({_activity.State})";
                WatchForOverflow();
            }
            else if (timer is { Mode: LiveActivityTimerMode.CountUp, StartsAt: { } overflowStart })
            {
                _appointmentEnd = overflowStart;
                status = $"Adopted overflow since {overflowStart:HH:mm:ss} ({_activity.State})";
            }
        }

        _statusLabel = new Label
        {
            AutomationId = "LiveActivityTimerStatus",
            Text = status
        };

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                _statusLabel,
                CreateButton("LiveActivityTimerStart", $"Start appointment ({_appointmentDuration.TotalMinutes:0} min)", StartAsync),
                CreateButton("LiveActivityTimerOverflowNow", "Overflow now", OverflowNowAsync),
                CreateButton("LiveActivityTimerEnd", "End", EndAsync)
            }
        };
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        CancelOverflowWatch();
    }

    private Button CreateButton(string automationId, string text, Func<Task> action)
        => new()
        {
            AutomationId = automationId,
            Text = text,
            Command = new Command(async () => await GuardAsync(action))
        };

    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async Task StartAsync()
    {
        CancelOverflowWatch();

        var now = DateTimeOffset.UtcNow;
        _appointmentEnd = now + _appointmentDuration;

        _activity = await _manager.StartAsync("appointment", new LiveActivityContent
        {
            Title = "Standup meeting",
            Subtitle = "Time remaining",
            AccentColor = "#30A46C",
            Timer = LiveActivityTimer.CountDown(_appointmentEnd, startedAt: now)
        });

        SetStatus($"Appointment until {_appointmentEnd:HH:mm:ss} ({_activity.State})");
        WatchForOverflow();
    }

    /// <summary>Moves the appointment end to now — instant overflow for quick manual testing.</summary>
    private async Task OverflowNowAsync()
    {
        if (_activity is null)
        {
            SetStatus("Not started");
            return;
        }

        _appointmentEnd = DateTimeOffset.UtcNow;
        CancelOverflowWatch();
        await EnterOverflowAsync();
    }

    private async Task EndAsync()
    {
        if (_activity is null)
        {
            SetStatus("Not started");
            return;
        }

        CancelOverflowWatch();
        await _activity.EndAsync(c =>
        {
            c.Subtitle = "Meeting over";
            c.Timer = null;
        });
        SetStatus($"Ended ({_activity.State})");
        _activity = null;
    }

    /// <summary>
    /// The boundary is app state, not OS behavior: the countdown display simply stops at
    /// zero (iOS) or goes negative (Android chronometer) — this loop is what turns
    /// "remaining" into "overflow" with a single semantic update.
    /// </summary>
    private void WatchForOverflow()
    {
        CancelOverflowWatch();
        var cts = new CancellationTokenSource();
        _overflowWatch = cts;

        _ = Task.Run(
            async () =>
            {
                var remaining = _appointmentEnd - DateTimeOffset.UtcNow;

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, cts.Token);
                }

                await Dispatcher.DispatchAsync(() => GuardAsync(EnterOverflowAsync));
            },
            cts.Token
        );
    }

    private async Task EnterOverflowAsync()
    {
        if (_activity is null || _activity.State == LiveActivityState.Ended)
        {
            return;
        }

        // Overflow = counting UP from the appointment end; the OS keeps ticking natively,
        // no further updates needed no matter how long the meeting runs over.
        await _activity.UpdateAsync(
            c =>
            {
                c.Subtitle = "Running over";
                c.AccentColor = "#E5484D";
                c.Timer = LiveActivityTimer.CountUp(_appointmentEnd);
            },
            new LiveActivityAlert("Appointment is running over")
        );

        SetStatus($"Overflowing since {_appointmentEnd:HH:mm:ss} ({_activity.State})");
    }

    private void CancelOverflowWatch()
    {
        _overflowWatch?.Cancel();
        _overflowWatch?.Dispose();
        _overflowWatch = null;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;
}
