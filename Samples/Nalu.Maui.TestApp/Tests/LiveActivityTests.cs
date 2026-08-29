using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Manual + UI-test harness for Nalu.Maui.LiveActivities: starts a "demo" activity with a
/// countdown timer and progress, then drives updates/alerts/end through the patch lambdas.
/// On Android 16+ the promoted notification shows the status-bar chip; on older Android it
/// degrades to a plain ongoing notification. On iOS the activity only renders when the app
/// bundles a widget extension, but the ActivityKit bridge calls are exercised regardless.
/// </summary>
[UsedImplicitly]
[TestPage("Live Activity Tests")]
public class LiveActivityTests : ContentPage
{
    private readonly ILiveActivityManager _manager;
    private readonly Label _statusLabel;
    private ILiveActivity? _activity;
    private double _progress;

    public LiveActivityTests(ILiveActivityManager manager)
    {
        _manager = manager;

        // Reconcile with activities that survived a process restart: adopt the running
        // "demo" activity instead of starting a duplicate next to it.
        _activity = _manager.Activities.LastOrDefault(a => a.Kind == "demo" && a.State != LiveActivityState.Ended);
        _progress = _activity?.Content.Progress?.Value ?? 0;

        _statusLabel = new Label
        {
            AutomationId = "LiveActivityStatus",
            Text = _activity is null
                ? $"Support: {_manager.Support}, activities: {_manager.Activities.Count}"
                : $"Adopted {_activity.Id} at {_progress * 100:0}% ({_activity.State})"
        };

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                _statusLabel,
                CreateButton("LiveActivityRequestPermission", "Request permission", RequestPermissionAsync),
                CreateButton("LiveActivityStart", "Start", StartAsync),
                CreateButton("LiveActivityAdvance", "Advance progress", AdvanceAsync),
                CreateButton("LiveActivityAlert", "Update with alert", AlertAsync),
                CreateButton("LiveActivityEnd", "End", EndAsync),
                CreateButton("LiveActivityEndImmediate", "End immediately", EndImmediateAsync)
            }
        };
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

    private async Task RequestPermissionAsync()
    {
        var granted = await _manager.RequestPermissionAsync();
        SetStatus($"Permission granted: {granted}, support: {_manager.Support}");
    }

    private async Task StartAsync()
    {
        _progress = 0.1;
        _activity = await _manager.StartAsync("demo", new LiveActivityContent
        {
            Title = "Nalu delivery",
            Subtitle = "Preparing your order",
            ChipText = "10%",
            AccentColor = "#4C7DF0",
            Progress = new LiveActivityProgress { Value = _progress },
            Timer = LiveActivityTimer.CountDown(DateTimeOffset.UtcNow.AddMinutes(5)),
            Actions =
            [
                // v1: link-backed actions render as buttons and open the app at the link.
                new LiveActivityAction { Id = "track", Label = "Track", Icon = "location.fill", DeepLink = "nalutest://track" },
                new LiveActivityAction { Id = "help", Label = "Help", DeepLink = "nalutest://help" },
                // Id-only action: reserved for direct callbacks (v2) — must NOT render in v1.
                new LiveActivityAction { Id = "ping", Label = "Ping" }
            ]
        });
        SetStatus($"Started {_activity.Id} ({_activity.State})");
    }

    private async Task AdvanceAsync()
    {
        if (_activity is null)
        {
            SetStatus("Not started");
            return;
        }

        _progress = Math.Min(1, _progress + 0.2);
        var chip = $"{_progress * 100:0}%";
        await _activity.UpdateAsync(c =>
        {
            c.Subtitle = "On the way";
            c.ChipText = chip;
            c.Progress!.Value = _progress;
        });
        SetStatus($"Progress {chip} ({_activity.State})");
    }

    private async Task AlertAsync()
    {
        if (_activity is null)
        {
            SetStatus("Not started");
            return;
        }

        await _activity.UpdateAsync(
            c => c.Subtitle = "Driver is at the door",
            new LiveActivityAlert("Driver arrived", "Meet them at the door")
        );
        SetStatus($"Alerted ({_activity.State})");
    }

    private async Task EndAsync()
    {
        if (_activity is null)
        {
            SetStatus("Not started");
            return;
        }

        await _activity.EndAsync(c =>
        {
            c.Title = "Delivered";
            c.Subtitle = "Enjoy!";
            c.ChipText = null;
            c.Progress = null;
            c.Timer = null;
        });
        SetStatus($"Ended ({_activity.State})");
    }

    private async Task EndImmediateAsync()
    {
        if (_activity is null)
        {
            SetStatus("Not started");
            return;
        }

        await _activity.EndAsync(finalPatch: null, LiveActivityDismissal.Immediate);
        SetStatus($"Ended immediately ({_activity.State})");
        _activity = null;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;
}
