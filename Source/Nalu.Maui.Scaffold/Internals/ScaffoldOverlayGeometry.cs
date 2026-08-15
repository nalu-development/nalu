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
}
