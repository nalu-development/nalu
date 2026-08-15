namespace Nalu;

/// <summary>
/// How a scaffold-hosted surface — a page, a bottom sheet, a popup — reacts to the soft keyboard.
/// Declared through <see cref="Scaffold.KeyboardModeProperty"/> (on a page, on the scaffold as the
/// app-wide page default, on sheet/popup content) or the sheet/popup call-site options. Exactly one
/// surface reacts at a time: the topmost presented sheet or popup, otherwise the page.
/// </summary>
public enum ScaffoldKeyboardMode
{
    /// <summary>
    /// The keyboard takes room away from the surface (the default): a page gets it as its bottom
    /// safe-area inset (it lays out above the keyboard as it does above the home indicator), a
    /// bottom sheet treats it as a bigger bottom inset — surface anchored to the bottom edge,
    /// content padded above the keyboard, detents unchanged — and a popup is re-placed in the area
    /// above it (a centered popup re-centers, an anchored one flips/clamps; it may get shorter to fit).
    /// </summary>
    Resize,

    /// <summary>
    /// The surface keeps its size and slides up by the LEAST it takes to keep the focused text
    /// input above the keyboard (the whole surface's overlap with the keyboard when no focused
    /// input can be located), never further than its own top edge allows. Android's
    /// <c>adjustPan</c> semantics; content below the focused input may end up under the keyboard.
    /// </summary>
    Pan,

    /// <summary>
    /// The surface ignores the keyboard entirely (its content may be covered). On a page this hands
    /// the keyboard back to MAUI: layouts with <c>SafeAreaEdges</c> <c>SoftInput</c> pad themselves.
    /// </summary>
    None
}
