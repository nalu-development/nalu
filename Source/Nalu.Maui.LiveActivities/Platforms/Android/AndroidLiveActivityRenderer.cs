using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using Android.OS;
using Application = Android.App.Application;

namespace Nalu;

/// <summary>
/// Maps the semantic <see cref="LiveActivityContent"/> onto an Android notification:
/// Android 16 (net10 bindings) gets ProgressStyle + status-bar chip + promotion,
/// everything else the classic progress bar / chronometer equivalents.
/// Notifications are only posted on API 26+ (notification channels), which is the
/// effective floor of this feature; below that the manager reports Unavailable.
/// </summary>
[SupportedOSPlatform("android26.0")]
internal static class AndroidLiveActivityRenderer
{
    private const int ProgressScale = 1000;

    public static Notification Render(
        AndroidLiveActivityManager manager,
        string id,
        string kind,
        LiveActivityContent content,
        string payload,
        LiveActivityAlert? alert,
        bool promoted,
        bool ongoing)
    {
        var context = Application.Context;
        var builder = new Notification.Builder(context, manager.GetChannelId(kind));

        builder
            .SetSmallIcon(ResolveSmallIcon(manager, context))
            .SetOngoing(ongoing)
            .SetOnlyAlertOnce(alert is null)
            .SetContentIntent(CreateContentIntent(context, content.DeepLink));

        if (alert is not null)
        {
            // The alert replaces title/subtitle for this post only: the ticker carries it
            // to accessibility services, the content shows it front and center.
            builder.SetTicker(alert.Title);
        }

        if (content.Title is not null)
        {
            builder.SetContentTitle(content.Title);
        }

        if (content.Subtitle is not null)
        {
            builder.SetContentText(content.Subtitle);
        }

        if (ParseColor(content.AccentColor) is { } accent)
        {
            builder.SetColor(accent);
            builder.SetColorized(false);
        }

        if (ResolveDrawableId(context, content.ImageName) is { } largeIconId)
        {
            builder.SetLargeIcon(Icon.CreateWithResource(context, largeIconId));
        }

        ApplyTimer(builder, content.Timer);
        AddActions(builder, context, content.Actions);

        var isModern = false;

#if NET10_0_OR_GREATER
        if (OperatingSystem.IsAndroidVersionAtLeast(36))
        {
            isModern = true;
            ApplyModernStyle(builder, context, content);

            if (promoted)
            {
                RequestPromotedOngoing(builder);
            }
        }
#endif

        if (!isModern)
        {
            ApplyClassicProgress(builder, content.Progress);
        }

        var extras = new Bundle();
        extras.PutString(AndroidLiveActivityManager.ExtraId, id);
        extras.PutString(AndroidLiveActivityManager.ExtraKind, kind);
        extras.PutString(AndroidLiveActivityManager.ExtraPayload, payload);

        // AddExtras merges — SetExtras would REPLACE the builder's bundle and wipe what
        // other builder APIs stored there (setRequestPromotedOngoing writes the promotion
        // request as the "android.requestPromotedOngoing" extra).
        builder.AddExtras(extras);

        return builder.Build();
    }

#if NET10_0_OR_GREATER
    /// <summary>
    /// Requests promotion to a Live Update (status-bar chip + floating card). The system sets
    /// FLAG_PROMOTED_ONGOING itself after validating the promotable characteristics and the
    /// POST_PROMOTED_NOTIFICATIONS permission — apps cannot set the flag directly (it is
    /// stripped at enqueue). Builder.requestPromotedOngoing(bool) is API 36.1 and not yet
    /// surfaced by the .NET bindings, so it is invoked through JNI; missing method (a 36.0
    /// device/image) downgrades gracefully to the un-promoted notification.
    /// </summary>
    private static Java.Lang.Reflect.Method? _requestPromotedOngoingMethod;
    private static bool _requestPromotedOngoingProbed;

    private static void RequestPromotedOngoing(Notification.Builder builder)
    {
        try
        {
            if (!_requestPromotedOngoingProbed)
            {
                _requestPromotedOngoingProbed = true;

                // QPR1 images ship setRequestPromotedOngoing; requestPromotedOngoing is the
                // name used by androidx/newer docs — accept either.
                _requestPromotedOngoingMethod = builder.Class.GetMethods()
                                                       .FirstOrDefault(static m => m.Name is "setRequestPromotedOngoing" or "requestPromotedOngoing");

                if (_requestPromotedOngoingMethod is null)
                {
                    Android.Util.Log.Info("NaluLiveActivities", "Notification.Builder.requestPromotedOngoing not found; posting without the status-bar chip.");
                }
            }

            _requestPromotedOngoingMethod?.Invoke(builder, Java.Lang.Boolean.ValueOf(true));
        }
        catch (Java.Lang.Throwable ex)
        {
            // 36.0 device without the QPR1 API: chip-less promoted styling still applies.
            Android.Util.Log.Info("NaluLiveActivities", $"requestPromotedOngoing failed: {ex.Message}");
        }
    }
#endif

#if NET10_0_OR_GREATER
    [SupportedOSPlatform("android36.0")]
    private static void ApplyModernStyle(Notification.Builder builder, Context context, LiveActivityContent content)
    {
        if (content.ChipText is not null)
        {
            builder.SetShortCriticalText(content.ChipText);
        }

        if (content.Progress is not { } progress)
        {
            return;
        }

        var style = new Notification.ProgressStyle();

        if (progress.Indeterminate)
        {
            style.SetProgressIndeterminate(true);
        }
        else if (progress.Value is { } value)
        {
            style.SetProgress(ToScaled(value));
        }

        if (progress.Segments is { Count: > 0 } segments)
        {
            var totalWeight = segments.Sum(static s => s.Weight);

            foreach (var segment in segments)
            {
                var platformSegment = new Notification.ProgressStyle.Segment((int)(segment.Weight / totalWeight * ProgressScale));

                if (ParseColor(segment.Color ?? content.AccentColor) is { } color)
                {
                    platformSegment.SetColor(new Android.Graphics.Color(color));
                }

                style.AddProgressSegment(platformSegment);
            }
        }
        else
        {
            var segment = new Notification.ProgressStyle.Segment(ProgressScale);

            if (ParseColor(content.AccentColor) is { } color)
            {
                segment.SetColor(new Android.Graphics.Color(color));
            }

            style.AddProgressSegment(segment);
        }

        if (progress.Points is { Count: > 0 } points)
        {
            foreach (var point in points)
            {
                style.AddProgressPoint(new Notification.ProgressStyle.Point(ToScaled(point.Position)));
            }
        }

        if (ResolveDrawableId(context, progress.TrackerIcon) is { } trackerIconId)
        {
            style.SetProgressTrackerIcon(Icon.CreateWithResource(context, trackerIconId));
        }

        builder.SetStyle(style);
    }
#endif

    private static void ApplyClassicProgress(Notification.Builder builder, LiveActivityProgress? progress)
    {
        if (progress is null)
        {
            return;
        }

        if (progress.Indeterminate)
        {
            builder.SetProgress(0, 0, true);
        }
        else if (progress.Value is { } value)
        {
            builder.SetProgress(ProgressScale, ToScaled(value), false);
        }
    }

    private static void ApplyTimer(Notification.Builder builder, LiveActivityTimer? timer)
    {
        switch (timer)
        {
            case { Mode: LiveActivityTimerMode.CountDown, EndsAt: { } endsAt }:
                builder.SetWhen(endsAt.ToUnixTimeMilliseconds()).SetShowWhen(true).SetUsesChronometer(true).SetChronometerCountDown(true);
                break;

            case { Mode: LiveActivityTimerMode.CountUp, StartsAt: { } startsAt }:
                builder.SetWhen(startsAt.ToUnixTimeMilliseconds()).SetShowWhen(true).SetUsesChronometer(true);
                break;

            case { Mode: LiveActivityTimerMode.Paused, PausedElapsed: { } elapsed }:
                builder.SetSubText(elapsed.ToString(elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss"));
                break;

            default:
                break;
        }
    }

    private static void AddActions(Notification.Builder builder, Context context, List<LiveActivityAction>? actions)
    {
        if (actions is null)
        {
            return;
        }

        foreach (var action in actions)
        {
            var icon = ResolveDrawableId(context, action.Icon) is { } iconId ? Icon.CreateWithResource(context, iconId) : null;
            var intent = CreateContentIntent(context, action.DeepLink);

            if (intent is not null)
            {
                builder.AddAction(new Notification.Action.Builder(icon, action.Label, intent).Build());
            }
        }
    }

    private static PendingIntent? CreateContentIntent(Context context, string? deepLink)
    {
        Intent? intent;

        if (deepLink is not null)
        {
            intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(deepLink));
            intent.SetPackage(context.PackageName);
            intent.AddFlags(ActivityFlags.NewTask);
        }
        else
        {
            intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
        }

        return intent is null
            ? null
            : PendingIntent.GetActivity(context, deepLink?.GetHashCode(StringComparison.Ordinal) ?? 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }

    private static int ResolveSmallIcon(AndroidLiveActivityManager manager, Context context)
        => manager.Options.AndroidSmallIcon != 0 ? manager.Options.AndroidSmallIcon : context.ApplicationInfo?.Icon ?? 0;

    private static int? ResolveDrawableId(Context context, string? name)
    {
        if (name is null)
        {
            return null;
        }

#pragma warning disable CA1422 // GetIdentifier is the only by-name lookup available
        var id = context.Resources?.GetIdentifier(name, "drawable", context.PackageName) ?? 0;
#pragma warning restore CA1422

        return id == 0 ? null : id;
    }

    private static int? ParseColor(string? hex)
    {
        if (hex is null || !hex.StartsWith('#') || hex.Length != 7 || !int.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return null;
        }

        return unchecked((int)0xFF000000 | rgb);
    }

    private static int ToScaled(double value) => (int)Math.Clamp(value * ProgressScale, 0, ProgressScale);
}
