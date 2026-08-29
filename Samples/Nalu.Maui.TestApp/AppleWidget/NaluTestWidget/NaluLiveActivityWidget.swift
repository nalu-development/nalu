import ActivityKit
import SwiftUI
import WidgetKit

// MARK: - Shared ActivityKit attributes

/// Copy of the attributes type defined by the Nalu.Maui.LiveActivities Swift bridge.
/// ActivityKit matches activities to widgets by the attributes TYPE NAME and Codable
/// shape, so this must stay identical to NaluLiveActivitiesInterop's declaration.
struct NaluLiveActivityAttributes: ActivityAttributes {
    struct ContentState: Codable, Hashable {
        var payload: String
    }

    var kind: String
}

// MARK: - The cross-platform content contract

/// The Nalu.Maui.LiveActivities content payload (camelCase JSON, epoch-ms instants) —
/// the same model the Android notification renderer consumes.
struct LiveContent: Codable {
    var title: String?
    var subtitle: String?
    var chipText: String?
    var chipIcon: String?
    var accentColor: String?
    var imageName: String?
    var deepLink: String?
    var progress: ProgressInfo?
    var timer: TimerInfo?
    var custom: [String: String]?

    struct ProgressInfo: Codable {
        var value: Double?
        var indeterminate: Bool?
    }

    struct TimerInfo: Codable {
        var mode: String?
        var startsAt: Double?
        var endsAt: Double?
        var pausedElapsed: Double?
    }

    static func decode(_ payload: String) -> LiveContent {
        guard let data = payload.data(using: .utf8),
              let content = try? JSONDecoder().decode(LiveContent.self, from: data) else {
            return LiveContent()
        }
        return content
    }

    var accent: Color {
        guard let accentColor,
              accentColor.hasPrefix("#"),
              accentColor.count == 7,
              let rgb = UInt32(accentColor.dropFirst(), radix: 16) else {
            return .accentColor
        }
        return Color(
            red: Double((rgb >> 16) & 0xFF) / 255,
            green: Double((rgb >> 8) & 0xFF) / 255,
            blue: Double(rgb & 0xFF) / 255
        )
    }

    var url: URL? {
        deepLink.flatMap(URL.init(string:))
    }
}

// MARK: - Widget

struct NaluLiveActivityWidget: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: NaluLiveActivityAttributes.self) { context in
            LockScreenView(content: LiveContent.decode(context.state.payload))
        } dynamicIsland: { context in
            let content = LiveContent.decode(context.state.payload)

            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    if let title = content.title {
                        Text(title)
                            .font(.headline)
                            .lineLimit(1)
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    TimerText(timer: content.timer)
                        .font(.headline)
                        .monospacedDigit()
                        .foregroundStyle(content.accent)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(alignment: .leading, spacing: 4) {
                        if let subtitle = content.subtitle {
                            Text(subtitle)
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        }
                        ProgressBar(content: content)
                    }
                }
            } compactLeading: {
                CompactLabel(content: content)
            } compactTrailing: {
                TimerText(timer: content.timer)
                    .monospacedDigit()
                    .foregroundStyle(content.accent)
                    .frame(maxWidth: 60)
            } minimal: {
                CompactLabel(content: content)
            }
            .widgetURL(content.url)
        }
    }
}

// MARK: - Views

private struct LockScreenView: View {
    let content: LiveContent

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    if let title = content.title {
                        Text(title)
                            .font(.headline)
                    }
                    if let subtitle = content.subtitle {
                        Text(subtitle)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }
                }
                Spacer()
                TimerText(timer: content.timer)
                    .font(.title3.weight(.semibold))
                    .monospacedDigit()
                    .foregroundStyle(content.accent)
            }
            ProgressBar(content: content)
        }
        .padding()
        .activityBackgroundTint(nil)
        .widgetURL(content.url)
    }
}

private struct ProgressBar: View {
    let content: LiveContent

    var body: some View {
        if let progress = content.progress {
            if progress.indeterminate == true {
                ProgressView()
                    .tint(content.accent)
            } else if let value = progress.value {
                ProgressView(value: min(max(value, 0), 1))
                    .tint(content.accent)
            }
        }
    }
}

private struct CompactLabel: View {
    let content: LiveContent

    var body: some View {
        if let chipText = content.chipText {
            Text(chipText)
                .font(.caption2.weight(.semibold))
                .foregroundStyle(content.accent)
        } else {
            Image(systemName: "circle.fill")
                .imageScale(.small)
                .foregroundStyle(content.accent)
        }
    }
}

/// The ticking clock, rendered natively by the OS: no updates while time passes.
private struct TimerText: View {
    let timer: LiveContent.TimerInfo?

    var body: some View {
        switch timer?.mode {
        case "CountDown":
            if let endsAt = timer?.endsAt {
                let end = Date(timeIntervalSince1970: endsAt / 1000)
                Text(timerInterval: Date.now...max(end, Date.now), countsDown: true)
            }

        case "CountUp":
            if let startsAt = timer?.startsAt {
                Text(Date(timeIntervalSince1970: startsAt / 1000), style: .timer)
            }

        case "Paused":
            if let elapsedMs = timer?.pausedElapsed {
                Text(Duration.seconds(elapsedMs / 1000), format: .time(pattern: .minuteSecond))
            }

        default:
            EmptyView()
        }
    }
}
