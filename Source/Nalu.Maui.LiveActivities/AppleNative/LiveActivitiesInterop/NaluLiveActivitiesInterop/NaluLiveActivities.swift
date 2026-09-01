import Foundation
#if canImport(ActivityKit)
import ActivityKit
#endif

/// The single generic ActivityKit attributes type shared by every Nalu live activity.
/// `kind` routes app-customized widget UIs; the dynamic state is one JSON payload
/// (the cross-platform `LiveActivityContent` contract) the widget decodes and renders.
@available(iOS 16.2, *)
public struct NaluLiveActivityAttributes: ActivityAttributes {
    public struct ContentState: Codable, Hashable {
        public var payload: String

        public init(payload: String) {
            self.payload = payload
        }
    }

    public var kind: String

    public init(kind: String) {
        self.kind = kind
    }
}

/// Objective-C visible bridge over ActivityKit consumed by the C# binding.
/// Results and errors travel through completion blocks; the activity list is
/// returned as one JSON string to keep the binding surface tiny.
@objc(NaluLiveActivitiesBridge)
public class NaluLiveActivitiesBridge: NSObject {

    /// Whether the OS supports Live Activities at all (iOS 16.2+).
    @objc(isSupported)
    public static func isSupported() -> Bool {
        if #available(iOS 16.2, *) {
            return true
        }
        return false
    }

    /// Whether the user allows this app to start Live Activities.
    @objc(areActivitiesEnabled)
    public static func areActivitiesEnabled() -> Bool {
        if #available(iOS 16.2, *) {
            return ActivityAuthorizationInfo().areActivitiesEnabled
        }
        return false
    }

    /// Starts a Live Activity; completes with (activityId, nil) or (nil, errorMessage).
    @objc(startActivity:payload:staleDateEpochMs:completion:)
    public static func startActivity(
        _ kind: String,
        payload: String,
        staleDateEpochMs: Double,
        completion: @escaping @Sendable (String?, String?) -> Void
    ) {
        guard #available(iOS 16.2, *) else {
            completion(nil, "Live Activities require iOS 16.2 or later.")
            return
        }

        do {
            let content = ActivityContent(
                state: NaluLiveActivityAttributes.ContentState(payload: payload),
                staleDate: date(fromEpochMs: staleDateEpochMs)
            )
            let activity = try Activity.request(
                attributes: NaluLiveActivityAttributes(kind: kind),
                content: content
            )
            completion(activity.id, nil)
        } catch {
            completion(nil, error.localizedDescription)
        }
    }

    /// Updates a running Live Activity; completes when ActivityKit has taken the update.
    @objc(updateActivity:payload:staleDateEpochMs:alertTitle:alertBody:completion:)
    public static func updateActivity(
        _ id: String,
        payload: String,
        staleDateEpochMs: Double,
        alertTitle: String?,
        alertBody: String?,
        completion: @escaping @Sendable () -> Void
    ) {
        guard #available(iOS 16.2, *) else {
            completion()
            return
        }

        Task {
            if let activity = findActivity(id) {
                let content = ActivityContent(
                    state: NaluLiveActivityAttributes.ContentState(payload: payload),
                    staleDate: date(fromEpochMs: staleDateEpochMs)
                )
                var alert: AlertConfiguration?
                if let alertTitle {
                    alert = AlertConfiguration(
                        title: LocalizedStringResource(stringLiteral: alertTitle),
                        body: LocalizedStringResource(stringLiteral: alertBody ?? ""),
                        sound: .default
                    )
                }
                await activity.update(content, alertConfiguration: alert)
            }
            completion()
        }
    }

    /// Ends a Live Activity with the final content; completes when it is dismissed/settled.
    @objc(endActivity:payload:immediate:completion:)
    public static func endActivity(
        _ id: String,
        payload: String,
        immediate: Bool,
        completion: @escaping @Sendable () -> Void
    ) {
        guard #available(iOS 16.2, *) else {
            completion()
            return
        }

        Task {
            if let activity = findActivity(id) {
                let content = ActivityContent(
                    state: NaluLiveActivityAttributes.ContentState(payload: payload),
                    staleDate: nil
                )
                await activity.end(content, dismissalPolicy: immediate ? .immediate : .default)
            }
            completion()
        }
    }

    /// Reports every activity state transition as (activityId, state) until the process
    /// dies — the live counterpart of `activitiesJson`, which only sees the state at
    /// startup. Newly started activities are picked up through `activityUpdates`, so one
    /// call at init covers activities started later too.
    @objc(observeActivityStates:)
    public static func observeActivityStates(_ callback: @escaping @Sendable (String, String) -> Void) {
        guard #available(iOS 16.2, *) else {
            return
        }

        for activity in Activity<NaluLiveActivityAttributes>.activities {
            track(activity.id, callback)
        }

        Task {
            for await activity in Activity<NaluLiveActivityAttributes>.activityUpdates {
                track(activity.id, callback)
            }
        }
    }

    /// The running activities as a JSON array of {id, kind, payload, state} objects,
    /// where state is "active" | "stale" | "dismissed" | "ended". Used for rehydration
    /// after restarts.
    @objc(activitiesJson)
    public static func activitiesJson() -> String {
        guard #available(iOS 16.2, *) else {
            return "[]"
        }

        let items: [[String: String]] = Activity<NaluLiveActivityAttributes>.activities.map { activity in
            [
                "id": activity.id,
                "kind": activity.attributes.kind,
                "payload": activity.content.state.payload,
                "state": stateName(activity.activityState)
            ]
        }

        guard let data = try? JSONSerialization.data(withJSONObject: items),
              let json = String(data: data, encoding: .utf8) else {
            return "[]"
        }
        return json
    }

    /// Pumps one activity's state transitions into the callback. The task ends by itself
    /// when ActivityKit finishes the sequence (the activity is gone for good).
    ///
    /// Only the id crosses into the Task: neither `Activity` nor its `activityStateUpdates`
    /// sequence is Sendable, so capturing either is a Swift 6 concurrency error. The handle
    /// is re-resolved on the other side instead.
    @available(iOS 16.2, *)
    private static func track(
        _ id: String,
        _ callback: @escaping @Sendable (String, String) -> Void
    ) {
        Task {
            guard let tracked = findActivity(id) else {
                return
            }

            for await state in tracked.activityStateUpdates {
                callback(id, stateName(state))
            }
        }
    }

    /// `dismissed` is the USER taking the activity off screen; `ended` is the app ending it.
    @available(iOS 16.2, *)
    private static func stateName(_ state: ActivityState) -> String {
        switch state {
        case .active:
            return "active"
        case .stale:
            return "stale"
        case .dismissed:
            return "dismissed"
        default:
            return "ended"
        }
    }

    @available(iOS 16.2, *)
    private static func findActivity(_ id: String) -> Activity<NaluLiveActivityAttributes>? {
        Activity<NaluLiveActivityAttributes>.activities.first { $0.id == id }
    }

    private static func date(fromEpochMs epochMs: Double) -> Date? {
        epochMs > 0 ? Date(timeIntervalSince1970: epochMs / 1000) : nil
    }
}
