package com.nalu.maui.liveactivities;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.Icon;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.service.notification.StatusBarNotification;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Native side of Nalu.Maui.LiveActivities: builds and posts the whole (promoted) ongoing
 * notification from the serialized cross-platform content payload in a single JNI call,
 * so per-update work never chatters across the managed/native boundary.
 *
 * The payload is the same camelCase JSON contract the iOS widget consumes; timestamps are
 * epoch milliseconds.
 */
public final class NaluLiveUpdates {

    private static final String EXTRA_ID = "nalu.live.id";
    private static final String EXTRA_KIND = "nalu.live.kind";
    private static final String EXTRA_PAYLOAD = "nalu.live.payload";
    private static final int PROGRESS_SCALE = 1000;

    private NaluLiveUpdates() {
    }

    /**
     * Ensures the channel exists, renders the payload and posts the notification.
     * Returns false when notifications cannot be posted (no manager / pre-O device).
     */
    public static boolean post(
        Context context,
        String tag,
        int notificationId,
        String activityId,
        String kind,
        String channelId,
        String channelName,
        int smallIcon,
        String payloadJson,
        boolean promoted,
        boolean ongoing,
        String alertTitle,
        String alertBody
    ) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return false;
        }

        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null) {
            return false;
        }

        if (manager.getNotificationChannel(channelId) == null) {
            manager.createNotificationChannel(new NotificationChannel(channelId, channelName, NotificationManager.IMPORTANCE_DEFAULT));
        }

        JSONObject payload;
        try {
            payload = new JSONObject(payloadJson);
        } catch (JSONException e) {
            payload = new JSONObject();
        }

        Notification.Builder builder = new Notification.Builder(context, channelId)
            .setSmallIcon(smallIcon != 0 ? smallIcon : context.getApplicationInfo().icon)
            .setOngoing(ongoing)
            .setOnlyAlertOnce(alertTitle == null)
            .setContentIntent(contentIntent(context, payload.optString("deepLink", null)));

        if (alertTitle != null) {
            builder.setTicker(alertTitle);
        }

        String title = payload.optString("title", null);
        if (title != null) {
            builder.setContentTitle(title);
        }

        String subtitle = payload.optString("subtitle", null);
        if (subtitle != null) {
            builder.setContentText(subtitle);
        }

        Integer accent = parseColor(payload.optString("accentColor", null));
        if (accent != null) {
            builder.setColor(accent);
            builder.setColorized(false);
        }

        Integer largeIcon = drawableId(context, payload.optString("imageName", null));
        if (largeIcon != null) {
            builder.setLargeIcon(Icon.createWithResource(context, largeIcon));
        }

        applyTimer(builder, payload.optJSONObject("timer"));
        applyActions(context, builder, payload.optJSONArray("actions"));

        JSONObject progress = payload.optJSONObject("progress");
        boolean modern = Build.VERSION.SDK_INT >= 36;

        if (modern) {
            applyModernStyle(context, builder, payload, progress, accent);

            if (promoted && Build.VERSION.SDK_INT_FULL >= Build.VERSION_CODES_FULL.BAKLAVA_1) {
                // Promotion to a Live Update (status-bar chip + floating card): the system
                // sets FLAG_PROMOTED_ONGOING itself after validating the promotable
                // characteristics and the POST_PROMOTED_NOTIFICATIONS permission.
                builder.setRequestPromotedOngoing(true);
            }
        } else if (progress != null) {
            if (progress.optBoolean("indeterminate", false)) {
                builder.setProgress(0, 0, true);
            } else if (progress.has("value")) {
                builder.setProgress(PROGRESS_SCALE, scaled(progress.optDouble("value", 0)), false);
            }
        }

        Bundle extras = new Bundle();
        extras.putString(EXTRA_ID, activityId);
        extras.putString(EXTRA_KIND, kind);
        extras.putString(EXTRA_PAYLOAD, payloadJson);
        // addExtras merges — setExtras would replace the builder's bundle and wipe what
        // other builder APIs stored there (setRequestPromotedOngoing among them).
        builder.addExtras(extras);

        manager.notify(tag, notificationId, builder.build());
        return true;
    }

    /** Cancels the notification of an activity. */
    public static void cancel(Context context, String tag, int notificationId) {
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager != null) {
            manager.cancel(tag, notificationId);
        }
    }

    /**
     * The live activities currently on screen as a JSON array of {id, kind, payload}
     * objects — the rehydration source after a process restart.
     */
    public static String getActiveJson(Context context, String tag) {
        JSONArray result = new JSONArray();
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);

        if (manager == null || Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return result.toString();
        }

        for (StatusBarNotification sbn : manager.getActiveNotifications()) {
            if (!tag.equals(sbn.getTag()) || sbn.getNotification() == null) {
                continue;
            }

            Bundle extras = sbn.getNotification().extras;
            String id = extras != null ? extras.getString(EXTRA_ID) : null;
            String kind = extras != null ? extras.getString(EXTRA_KIND) : null;
            String payload = extras != null ? extras.getString(EXTRA_PAYLOAD) : null;

            if (id == null || kind == null || payload == null) {
                continue;
            }

            try {
                JSONObject item = new JSONObject();
                item.put("id", id);
                item.put("kind", kind);
                item.put("payload", payload);
                result.put(item);
            } catch (JSONException ignored) {
                // Skip the malformed entry.
            }
        }

        return result.toString();
    }

    private static void applyModernStyle(Context context, Notification.Builder builder, JSONObject payload, JSONObject progress, Integer accent) {
        String chipText = payload.optString("chipText", null);
        if (chipText != null) {
            builder.setShortCriticalText(chipText);
        }

        if (progress == null) {
            return;
        }

        Notification.ProgressStyle style = new Notification.ProgressStyle();

        if (progress.optBoolean("indeterminate", false)) {
            style.setProgressIndeterminate(true);
        } else if (progress.has("value")) {
            style.setProgress(scaled(progress.optDouble("value", 0)));
        }

        JSONArray segments = progress.optJSONArray("segments");
        if (segments != null && segments.length() > 0) {
            double totalWeight = 0;
            for (int i = 0; i < segments.length(); i++) {
                totalWeight += segments.optJSONObject(i) != null ? segments.optJSONObject(i).optDouble("weight", 1) : 1;
            }

            for (int i = 0; i < segments.length(); i++) {
                JSONObject segment = segments.optJSONObject(i);
                double weight = segment != null ? segment.optDouble("weight", 1) : 1;
                Notification.ProgressStyle.Segment platformSegment =
                    new Notification.ProgressStyle.Segment((int) (weight / totalWeight * PROGRESS_SCALE));

                Integer color = parseColor(segment != null ? segment.optString("color", null) : null);
                if (color == null) {
                    color = accent;
                }
                if (color != null) {
                    platformSegment.setColor(color);
                }

                style.addProgressSegment(platformSegment);
            }
        } else {
            Notification.ProgressStyle.Segment segment = new Notification.ProgressStyle.Segment(PROGRESS_SCALE);
            if (accent != null) {
                segment.setColor(accent);
            }
            style.addProgressSegment(segment);
        }

        JSONArray points = progress.optJSONArray("points");
        if (points != null) {
            for (int i = 0; i < points.length(); i++) {
                JSONObject point = points.optJSONObject(i);
                if (point != null) {
                    style.addProgressPoint(new Notification.ProgressStyle.Point(scaled(point.optDouble("position", 0))));
                }
            }
        }

        Integer trackerIcon = drawableId(context, progress.optString("trackerIcon", null));
        if (trackerIcon != null) {
            style.setProgressTrackerIcon(Icon.createWithResource(context, trackerIcon));
        }

        builder.setStyle(style);
    }

    private static void applyTimer(Notification.Builder builder, JSONObject timer) {
        if (timer == null) {
            return;
        }

        String mode = timer.optString("mode", "");
        long startsAt = timer.optLong("startsAt", 0);
        long endsAt = timer.optLong("endsAt", 0);

        if ("CountDown".equals(mode) && endsAt > 0) {
            builder.setWhen(endsAt).setShowWhen(true).setUsesChronometer(true).setChronometerCountDown(true);
        } else if ("CountUp".equals(mode) && startsAt > 0) {
            builder.setWhen(startsAt).setShowWhen(true).setUsesChronometer(true);
        } else if ("Paused".equals(mode) && timer.has("pausedElapsed")) {
            long elapsedSeconds = timer.optLong("pausedElapsed", 0) / 1000;
            long hours = elapsedSeconds / 3600;
            long minutes = (elapsedSeconds % 3600) / 60;
            long seconds = elapsedSeconds % 60;
            builder.setSubText(hours > 0
                ? String.format(java.util.Locale.ROOT, "%d:%02d:%02d", hours, minutes, seconds)
                : String.format(java.util.Locale.ROOT, "%d:%02d", minutes, seconds));
        }
    }

    private static void applyActions(Context context, Notification.Builder builder, JSONArray actions) {
        if (actions == null) {
            return;
        }

        for (int i = 0; i < actions.length(); i++) {
            JSONObject action = actions.optJSONObject(i);
            if (action == null) {
                continue;
            }

            String label = action.optString("label", null);
            PendingIntent intent = contentIntent(context, action.optString("deepLink", null));
            if (label == null || intent == null) {
                continue;
            }

            Integer iconId = drawableId(context, action.optString("icon", null));
            Icon icon = iconId != null ? Icon.createWithResource(context, iconId) : null;
            builder.addAction(new Notification.Action.Builder(icon, label, intent).build());
        }
    }

    private static PendingIntent contentIntent(Context context, String deepLink) {
        Intent intent;

        if (deepLink != null) {
            intent = new Intent(Intent.ACTION_VIEW, Uri.parse(deepLink));
            intent.setPackage(context.getPackageName());
            intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        } else {
            intent = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        }

        return intent == null
            ? null
            : PendingIntent.getActivity(
                context,
                deepLink != null ? deepLink.hashCode() : 0,
                intent,
                PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
    }

    private static Integer drawableId(Context context, String name) {
        if (name == null) {
            return null;
        }

        int id = context.getResources().getIdentifier(name, "drawable", context.getPackageName());
        return id == 0 ? null : id;
    }

    private static Integer parseColor(String hex) {
        if (hex == null) {
            return null;
        }

        try {
            return Color.parseColor(hex);
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    private static int scaled(double value) {
        return (int) Math.max(0, Math.min(PROGRESS_SCALE, value * PROGRESS_SCALE));
    }
}
