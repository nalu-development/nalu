namespace Nalu;

/// <summary>
/// How a scaffold-hosted surface reacts to the soft keyboard. Declared per bottom sheet / popup
/// through <see cref="Scaffold.KeyboardModeProperty"/> on the content (or the call-site options);
/// the same vocabulary is meant to describe pages next.
/// </summary>
public enum ScaffoldKeyboardMode
{
    /// <summary>
    /// The keyboard takes room away from the surface (the default): a bottom sheet treats it as
    /// a bigger bottom inset — surface anchored to the bottom edge, content padded above the
    /// keyboard, detents unchanged — and a popup is re-placed in the area above it (a centered
    /// popup re-centers, an anchored one flips/clamps; it may get shorter to fit).
    /// </summary>
    Resize,

    /// <summary>
    /// The surface keeps its size and slides up by the LEAST it takes to keep the focused text
    /// input above the keyboard (the whole surface's overlap with the keyboard when no focused
    /// input can be located), never further than its own top edge allows. Android's
    /// <c>adjustPan</c> semantics; content below the focused input may end up under the keyboard.
    /// </summary>
    Pan,

    /// <summary>The surface ignores the keyboard entirely (its content may be covered).</summary>
    None
}
