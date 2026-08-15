namespace Nalu;

/// <summary>
/// The shared keyboard-vs-overlay math (device-independent units): presenters supply the system
/// bottom inset and the soft keyboard's overlap with the scaffold container, measured from its
/// bottom edge (0 while hidden — iOS reads it from <c>UIView.keyboardLayoutGuide</c>, Android from
/// the IME window insets), and lay sheets and popups out in the area ABOVE the keyboard.
/// </summary>
/// <remarks>
/// A visible keyboard covers the bottom system bar region, so wherever it is the keyboard REPLACES
/// the system inset rather than adding to it: a popup's placement area ends at the keyboard's top
/// edge, and a bottom sheet treats the keyboard as a (much) bigger bottom safe-area inset — the
/// sheet surface stays anchored to the window's bottom edge, continuous behind the keyboard, while
/// its CONTENT is padded up to the keyboard's top edge (the same mechanism that keeps the content
/// clear of the home indicator / navigation bar when there is no keyboard).
/// </remarks>
internal static class ScaffoldOverlayGeometry
{
    /// <summary>The bottom inset a placement area must respect: the larger of the system inset and the keyboard overlap.</summary>
    public static double BottomInset(double systemBottomInset, double keyboardOverlap)
        => Math.Max(systemBottomInset, keyboardOverlap);

    /// <summary>
    /// A bottom sheet's bottom content padding: the system inset while it rests on the bottom edge,
    /// the keyboard's overlap while one is up (it covers the system bar region).
    /// </summary>
    public static double SheetBottomPadding(double systemBottomInset, double keyboardOverlap)
        => BottomInset(systemBottomInset, keyboardOverlap);

    /// <summary>Gap kept between a panned surface's focused input and the keyboard's top edge.</summary>
    public const double PanGap = 8;

    /// <summary>
    /// <see cref="ScaffoldKeyboardMode.Pan"/>: how far up a surface slides — the least that keeps
    /// the focused input (its bottom edge, in the surface's UNPANNED container coordinates) above
    /// the keyboard's top edge, or the surface's own overlap with the keyboard when no focused
    /// input is known — never past the surface's top edge reaching <paramref name="minTop"/>.
    /// </summary>
    /// <param name="keyboardTop">The keyboard's top edge in container coordinates (container height − overlap).</param>
    /// <param name="surfaceTop">The surface's visible top edge, unpanned.</param>
    /// <param name="surfaceBottom">The surface's bottom edge, unpanned.</param>
    /// <param name="focusedInputBottom">The focused input's bottom edge, unpanned; null when unknown.</param>
    /// <param name="minTop">The highest edge the surface may be panned to (the top system inset).</param>
    public static double Pan(double keyboardTop, double surfaceTop, double surfaceBottom, double? focusedInputBottom, double minTop)
    {
        var needed = focusedInputBottom is { } focused
            ? focused + PanGap - keyboardTop
            : surfaceBottom - keyboardTop;

        var maxPan = Math.Max(0, surfaceTop - minTop);

        return Math.Clamp(needed, 0, maxPan);
    }
}
