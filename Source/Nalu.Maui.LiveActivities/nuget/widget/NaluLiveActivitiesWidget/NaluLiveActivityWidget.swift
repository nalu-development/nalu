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
    var actions: [ActionInfo]?
    var custom: [String: String]?

    struct ProgressInfo: Codable {
        var value: Double?
        var indeterminate: Bool?
        var segments: [SegmentInfo]?
        var points: [PointInfo]?
    }

    struct SegmentInfo: Codable {
        var weight: Double?
        var color: String?
    }

    struct PointInfo: Codable {
        var position: Double?
    }

    struct ActionInfo: Codable {
        var id: String?
        var label: String?
        var icon: String?
        var deepLink: String?
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

    /// Identity glyph for the card and the compact island: chipIcon is interpreted as an
    /// SF Symbol name on iOS (on Android it is a drawable resource name).
    var symbol: String? {
        chipIcon
    }

    /// v1 renders only link-backed actions; id-only actions are reserved for the
    /// upcoming direct-callback support.
    var renderableActions: [(String, String?, URL)] {
        (actions ?? []).compactMap { action in
            guard let label = action.label, let link = action.deepLink, let url = URL(string: link) else {
                return nil
            }
            return (label, action.icon, url)
        }
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
                // The top row flanks the sensor housing (Android-card header shape):
                // identity glyph leading, ticking timer trailing. Title/subtitle/track/
                // actions get the full width below — long titles never truncate.
                DynamicIslandExpandedRegion(.leading) {
                    if let symbol = content.symbol {
                        Image(systemName: symbol)
                            .font(.title3.weight(.semibold))
                            .foregroundStyle(content.accent)
                            .frame(width: 30, height: 30)
                            .background(content.accent.opacity(0.16), in: Circle())
                    }
                }
                DynamicIslandExpandedRegion(.trailing) {
                    // Scale down instead of truncating: overflow grows ("−1:02:03") and
                    // an ellipsised clock is worse than a smaller one.
                    TimerText(timer: content.timer)
                        .font(.system(.headline, design: .rounded).weight(.bold))
                        .monospacedDigit()
                        .lineLimit(1)
                        .minimumScaleFactor(0.5)
                        .multilineTextAlignment(.trailing)
                        .frame(maxWidth: 84, alignment: .trailing)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(alignment: .leading, spacing: 8) {
                        VStack(alignment: .leading, spacing: 1) {
                            if let title = content.title {
                                Text(title)
                                    .font(.headline)
                                    .lineLimit(1)
                                    .minimumScaleFactor(0.85)
                            }
                            if let subtitle = content.subtitle {
                                Text(subtitle)
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                                    .lineLimit(1)
                            }
                        }

                        ProgressTrack(content: content)

                        ActionRow(content: content)
                    }
                    .padding(.top, 2)
                    .padding(.bottom, 4)
                }
            } compactLeading: {
                CompactLabel(content: content)
            } compactTrailing: {
                // Text(timerInterval:) is width-greedy: align the digits to the trailing
                // edge so the reserved width never reads as asymmetric padding.
                TimerText(timer: content.timer)
                    .monospacedDigit()
                    .lineLimit(1)
                    .minimumScaleFactor(0.6)
                    .multilineTextAlignment(.trailing)
                    .frame(maxWidth: 52, alignment: .trailing)
            } minimal: {
                CompactLabel(content: content)
            }
            .widgetURL(content.url)
        }
    }
}

// MARK: - Views
//
// One design language on both platforms: Android's promoted Live Update is a
// system-templated card (identity + title/subtitle, time trailing, tinted segmented
// ProgressStyle bar), so the iOS surfaces mirror that card with native polish —
// rounded bold ticking timer, secondary styles that adapt to Lock Screen / island,
// and a segmented capsule track that echoes Android 16's signature progress look.

private struct LockScreenView: View {
    let content: LiveContent

    var body: some View {
        ContentCard(content: content)
            .padding(.horizontal, 18)
            .padding(.vertical, 16)
            .activityBackgroundTint(nil)
            .widgetURL(content.url)
    }
}

/// The shared full-width card: title owns the width (never truncated by the clock),
/// the ticking timer sits large and trailing, the segmented track closes the card.
private struct ContentCard: View {
    let content: LiveContent

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .center, spacing: 12) {
                if let symbol = content.symbol {
                    Image(systemName: symbol)
                        .font(.title3.weight(.semibold))
                        .foregroundStyle(content.accent)
                        .frame(width: 34, height: 34)
                        .background(content.accent.opacity(0.16), in: Circle())
                }

                VStack(alignment: .leading, spacing: 1) {
                    if let title = content.title {
                        Text(title)
                            .font(.headline)
                            .lineLimit(1)
                            .minimumScaleFactor(0.85)
                    }
                    if let subtitle = content.subtitle {
                        Text(subtitle)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                            .lineLimit(1)
                    }
                }
                .layoutPriority(1)

                Spacer(minLength: 8)

                TimerText(timer: content.timer)
                    .font(.system(.title2, design: .rounded).weight(.bold))
                    .monospacedDigit()
                    .lineLimit(1)
                    .minimumScaleFactor(0.5)
                    .multilineTextAlignment(.trailing)
                    .frame(maxWidth: 92, alignment: .trailing)
            }

            ProgressTrack(content: content)

            ActionRow(content: content)
        }
    }
}

/// Deep-link action buttons, mirroring the Android notification actions: tapping opens
/// the app at the action's link (in-place callbacks arrive with the v2 action support).
private struct ActionRow: View {
    let content: LiveContent

    var body: some View {
        let actions = content.renderableActions

        if !actions.isEmpty {
            HStack(spacing: 8) {
                ForEach(Array(actions.enumerated()), id: \.offset) { _, action in
                    let (label, icon, url) = action

                    Link(destination: url) {
                        HStack(spacing: 5) {
                            if let icon {
                                Image(systemName: icon)
                                    .imageScale(.small)
                            }
                            Text(label)
                                .font(.footnote.weight(.semibold))
                                .lineLimit(1)
                        }
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .foregroundStyle(.primary)
                        .background(.quaternary, in: Capsule())
                    }
                }
            }
            .padding(.top, 2)
        }
    }
}

/// Android-16-style segmented progress: weighted capsule segments separated by small
/// gaps, filled up to the current fraction, with milestone dots. Renders nothing when
/// the content carries no progress.
private struct ProgressTrack: View {
    let content: LiveContent

    var body: some View {
        if let progress = content.progress {
            if progress.indeterminate == true {
                ProgressView()
                    .progressViewStyle(.linear)
                    .tint(content.accent)
            } else if let value = progress.value {
                SegmentedBar(
                    value: min(max(value, 0), 1),
                    segments: normalizedSegments,
                    points: (content.progress?.points ?? []).compactMap { $0.position },
                    accent: content.accent
                )
                .frame(height: 6)
            }
        }
    }

    /// (startFraction, endFraction, color) per segment; a single accent segment when none given.
    private var normalizedSegments: [(Double, Double, Color)] {
        guard let segments = content.progress?.segments, !segments.isEmpty else {
            return [(0, 1, content.accent)]
        }

        let total = segments.reduce(0.0) { $0 + max($1.weight ?? 1, 0.0001) }
        var start = 0.0

        return segments.map { segment in
            let end = start + max(segment.weight ?? 1, 0.0001) / total
            defer { start = end }
            return (start, end, Color(hex: segment.color) ?? content.accent)
        }
    }
}

private struct SegmentedBar: View {
    let value: Double
    let segments: [(Double, Double, Color)]
    let points: [Double]
    let accent: Color

    private let gap = 3.0

    var body: some View {
        GeometryReader { geo in
            let width = geo.size.width
            let height = geo.size.height

            ZStack(alignment: .leading) {
                ForEach(Array(segments.enumerated()), id: \.offset) { _, segment in
                    let (start, end, color) = segment
                    let x = start * width + (start > 0 ? gap / 2 : 0)
                    let segmentWidth = max((end - start) * width - (start > 0 ? gap / 2 : 0) - (end < 1 ? gap / 2 : 0), 1)
                    let fill = min(max((value - start) / (end - start), 0), 1)

                    ZStack(alignment: .leading) {
                        Capsule().fill(color.opacity(0.22))

                        if fill > 0 {
                            Capsule()
                                .fill(color)
                                .frame(width: max(fill * segmentWidth, height))
                        }
                    }
                    .frame(width: segmentWidth, height: height)
                    .offset(x: x)
                }

                ForEach(Array(points.enumerated()), id: \.offset) { _, position in
                    Circle()
                        .fill(position <= value ? accent : Color.primary.opacity(0.25))
                        .frame(width: height, height: height)
                        .offset(x: min(max(position, 0), 1) * (width - height))
                }
            }
        }
    }
}

/// The tiny always-visible identity — mirrors the Android status-bar chip
/// (accent pill with the short text, falling back to a dot).
private struct CompactLabel: View {
    let content: LiveContent

    var body: some View {
        if let chipText = content.chipText {
            Text(chipText)
                .font(.caption2.weight(.bold))
                .monospacedDigit()
                .foregroundStyle(.primary)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(.quaternary, in: Capsule())
        } else {
            Image(systemName: content.symbol ?? "circle.fill")
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
                if end > Date.now {
                    Text(timerInterval: Date.now...end, countsDown: true)
                } else {
                    // Already ran over at render time: mirror Android's negative
                    // chronometer (count up from the end, negated). Note the platform
                    // limit: this view only re-renders on content updates, so a
                    // countdown crossing zero holds at 0:00 until the next update —
                    // apps wanting an exact boundary flip should update at the end
                    // instant (see the appointment pattern).
                    Text(verbatim: "−") + Text(end, style: .timer)
                }
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

private extension Color {
    init?(hex: String?) {
        guard let hex, hex.hasPrefix("#"), hex.count == 7, let rgb = UInt32(hex.dropFirst(), radix: 16) else {
            return nil
        }
        self.init(
            red: Double((rgb >> 16) & 0xFF) / 255,
            green: Double((rgb >> 8) & 0xFF) / 255,
            blue: Double(rgb & 0xFF) / 255
        )
    }
}
