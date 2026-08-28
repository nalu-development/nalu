namespace Nalu.Internals;

/// <summary>
/// A snapshot of the platform scroller's geometry in device-independent units, used to translate
/// descendant-targeting scroll requests into content distances.
/// </summary>
/// <param name="ViewportWidth">The full viewport width.</param>
/// <param name="ViewportHeight">The full viewport height.</param>
/// <param name="VisibleWidth">The viewport width minus the leading/trailing safe-area insets reserved by the platform.</param>
/// <param name="VisibleHeight">The viewport height minus the leading/trailing safe-area insets reserved by the platform.</param>
/// <param name="ContentWidth">The width of the scrollable canvas (content + <see cref="ScrollBox" /> padding, excluding safe-area insets).</param>
/// <param name="ContentHeight">The height of the scrollable canvas (content + <see cref="ScrollBox" /> padding, excluding safe-area insets).</param>
/// <param name="ScrollX">The current horizontal distance scrolled from the start of the content.</param>
/// <param name="ScrollY">The current vertical distance scrolled from the start of the content.</param>
internal readonly record struct ScrollBoxGeometry(
    double ViewportWidth,
    double ViewportHeight,
    double VisibleWidth,
    double VisibleHeight,
    double ContentWidth,
    double ContentHeight,
    double ScrollX,
    double ScrollY
);
