package com.nalu.maui.liveactivities;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.graphics.drawable.Icon;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.service.notification.StatusBarNotification;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * Native side of Nalu.Maui.LiveActivities: builds and posts the whole (promoted) ongoing
 * notification in a single JNI call. The content arrives as typed arguments — flattened
 * primitives, strings and primitive arrays — so nothing is parsed on the hot path; the
 * serialized payload is carried along as an OPAQUE string only to be stored in the
 * notification extras for rehydration after a process restart.
 */
public final class NaluLiveUpdates {

    /**
     * Broadcast fired when the USER removes a live activity (swipe, or "Clear all").
     * Sent with this app's own identity and package-scoped, so a runtime-registered
     * NOT_EXPORTED receiver is the intended listener. NotificationManager.cancel() —
     * how the app itself takes an activity down — deliberately does NOT fire it.
     */
    public static final String ACTION_DISMISSED = "com.nalu.maui.liveactivities.DISMISSED";

    /** Carries the live activity id, both in the notification extras and on {@link #ACTION_DISMISSED}. */
    public static final String EXTRA_ID = "nalu.live.id";

    private static final String EXTRA_KIND = "nalu.live.kind";
    private static final String EXTRA_PAYLOAD = "nalu.live.payload";
    private static final int PROGRESS_SCALE = 1000;

    /** progressMode values. */
    public static final int PROGRESS_NONE = 0;
    public static final int PROGRESS_VALUE = 1;
    public static final int PROGRESS_INDETERMINATE = 2;

    /** timerMode values. */
    public static final int TIMER_NONE = 0;
    public static final int TIMER_COUNT_DOWN = 1;
    public static final int TIMER_COUNT_UP = 2;
    public static final int TIMER_PAUSED = 3;

    private NaluLiveUpdates() {
    }

    /**
     * Whether this device can PROMOTE an ongoing notification to a Live Update
     * (status-bar chip + floating card). The promotion API only exists from
     * Android 16 QPR1 (SDK_INT_FULL BAKLAVA_1 = API 36.1); base Android 16
     * renders the same content as a plain ongoing notification.
     */
    public static boolean supportsPromotion() {
        return Build.VERSION.SDK_INT_FULL >= Build.VERSION_CODES_FULL.BAKLAVA_1;
    }

    /**
     * Ensures the channel exists, renders the content and posts the notification.
     * Colors are ARGB ints with 0 meaning "not set" (real colors always carry alpha).
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
        String title,
        String subtitle,
        String chipText,
        int accentColor,
        String imageName,
        int progressMode,
        double progressValue,
        double[] segmentWeights,
        int[] segmentColors,
        double[] pointPositions,
        String trackerIcon,
        int timerMode,
        long timerAnchorMs,
        long pausedElapsedMs,
        String deepLink,
        String[] actionLabels,
        String[] actionDeepLinks,
        String[] actionIcons,
        boolean promoted,
        boolean ongoing,
        String alertTitle,
        String payload
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

        Notification.Builder builder = new Notification.Builder(context, channelId)
            .setSmallIcon(smallIcon != 0 ? smallIcon : context.getApplicationInfo().icon)
            .setOngoing(ongoing)
            .setOnlyAlertOnce(alertTitle == null)
            .setContentIntent(contentIntent(context, deepLink))
            .setDeleteIntent(deleteIntent(context, notificationId, activityId));

        if (alertTitle != null) {
            builder.setTicker(alertTitle);
        }

        if (title != null) {
            builder.setContentTitle(title);
        }

        if (subtitle != null) {
            builder.setContentText(subtitle);
        }

        if (accentColor != 0) {
            builder.setColor(accentColor);
            builder.setColorized(false);
        }

        int largeIcon = drawableId(context, imageName);
        if (largeIcon != 0) {
            builder.setLargeIcon(Icon.createWithResource(context, largeIcon));
        }

        applyTimer(builder, timerMode, timerAnchorMs, pausedElapsedMs);
        applyActions(context, builder, actionLabels, actionDeepLinks, actionIcons);

        if (Build.VERSION.SDK_INT >= 36) {
            if (chipText != null) {
                builder.setShortCriticalText(chipText);
            }

            if (progressMode != PROGRESS_NONE) {
                builder.setStyle(progressStyle(context, progressMode, progressValue, segmentWeights, segmentColors, pointPositions, trackerIcon, accentColor));
            }

            if (promoted && Build.VERSION.SDK_INT_FULL >= Build.VERSION_CODES_FULL.BAKLAVA_1) {
                // Promotion to a Live Update (status-bar chip + floating card): the system
                // sets FLAG_PROMOTED_ONGOING itself after validating the promotable
                // characteristics and the POST_PROMOTED_NOTIFICATIONS permission.
                builder.setRequestPromotedOngoing(true);
            }
        } else if (progressMode == PROGRESS_INDETERMINATE) {
            builder.setProgress(0, 0, true);
        } else if (progressMode == PROGRESS_VALUE) {
            builder.setProgress(PROGRESS_SCALE, scaled(progressValue), false);
        }

        Bundle extras = new Bundle();
        extras.putString(EXTRA_ID, activityId);
        extras.putString(EXTRA_KIND, kind);
        extras.putString(EXTRA_PAYLOAD, payload);
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
     * objects — the rehydration source after a process restart (cold path; the payload
     * is the same opaque string handed to {@link #post}).
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

    private static Notification.ProgressStyle progressStyle(
        Context context,
        int progressMode,
        double progressValue,
        double[] segmentWeights,
        int[] segmentColors,
        double[] pointPositions,
        String trackerIcon,
        int accentColor
    ) {
        Notification.ProgressStyle style = new Notification.ProgressStyle();

        if (progressMode == PROGRESS_INDETERMINATE) {
            style.setProgressIndeterminate(true);
        } else {
            style.setProgress(scaled(progressValue));
        }

        if (segmentWeights != null && segmentWeights.length > 0) {
            double totalWeight = 0;
            for (double weight : segmentWeights) {
                totalWeight += weight;
            }

            for (int i = 0; i < segmentWeights.length; i++) {
                Notification.ProgressStyle.Segment segment =
                    new Notification.ProgressStyle.Segment((int) (segmentWeights[i] / totalWeight * PROGRESS_SCALE));

                int color = segmentColors != null && i < segmentColors.length && segmentColors[i] != 0 ? segmentColors[i] : accentColor;
                if (color != 0) {
                    segment.setColor(color);
                }

                style.addProgressSegment(segment);
            }
        } else {
            Notification.ProgressStyle.Segment segment = new Notification.ProgressStyle.Segment(PROGRESS_SCALE);
            if (accentColor != 0) {
                segment.setColor(accentColor);
            }
            style.addProgressSegment(segment);
        }

        if (pointPositions != null) {
            for (double position : pointPositions) {
                style.addProgressPoint(new Notification.ProgressStyle.Point(scaled(position)));
            }
        }

        int trackerIconId = drawableId(context, trackerIcon);
        if (trackerIconId != 0) {
            style.setProgressTrackerIcon(Icon.createWithResource(context, trackerIconId));
        }

        return style;
    }

    private static void applyTimer(Notification.Builder builder, int timerMode, long timerAnchorMs, long pausedElapsedMs) {
        switch (timerMode) {
            case TIMER_COUNT_DOWN:
                builder.setWhen(timerAnchorMs).setShowWhen(true).setUsesChronometer(true).setChronometerCountDown(true);
                break;

            case TIMER_COUNT_UP:
                builder.setWhen(timerAnchorMs).setShowWhen(true).setUsesChronometer(true);
                break;

            case TIMER_PAUSED:
                long elapsedSeconds = pausedElapsedMs / 1000;
                long hours = elapsedSeconds / 3600;
                long minutes = (elapsedSeconds % 3600) / 60;
                long seconds = elapsedSeconds % 60;
                builder.setSubText(hours > 0
                    ? String.format(java.util.Locale.ROOT, "%d:%02d:%02d", hours, minutes, seconds)
                    : String.format(java.util.Locale.ROOT, "%d:%02d", minutes, seconds));
                break;

            default:
                break;
        }
    }

    private static void applyActions(Context context, Notification.Builder builder, String[] labels, String[] deepLinks, String[] icons) {
        if (labels == null || deepLinks == null) {
            return;
        }

        for (int i = 0; i < labels.length && i < deepLinks.length; i++) {
            PendingIntent intent = contentIntent(context, deepLinks[i]);
            if (labels[i] == null || intent == null) {
                continue;
            }

            int iconId = icons != null && i < icons.length ? drawableId(context, icons[i]) : 0;
            Icon icon = iconId != 0 ? Icon.createWithResource(context, iconId) : null;
            builder.addAction(new Notification.Action.Builder(icon, labels[i], intent).build());
        }
    }

    /**
     * Fires {@link #ACTION_DISMISSED} when the user swipes the notification away, so the
     * managed side can stop pushing updates to something no longer on screen. Keyed by
     * notificationId so each activity gets its own PendingIntent, and UPDATE_CURRENT keeps
     * the extras fresh across the re-posts that every content update performs.
     */
    private static PendingIntent deleteIntent(Context context, int notificationId, String activityId) {
        Intent intent = new Intent(ACTION_DISMISSED)
            .setPackage(context.getPackageName())
            .putExtra(EXTRA_ID, activityId);

        return PendingIntent.getBroadcast(
            context,
            notificationId,
            intent,
            PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
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

    private static int drawableId(Context context, String name) {
        return name == null ? 0 : context.getResources().getIdentifier(name, "drawable", context.getPackageName());
    }

    private static int scaled(double value) {
        return (int) Math.max(0, Math.min(PROGRESS_SCALE, value * PROGRESS_SCALE));
    }
}
