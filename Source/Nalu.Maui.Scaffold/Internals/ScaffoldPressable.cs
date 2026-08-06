namespace Nalu.Internals;

/// <summary>
/// Raw touch-DOWN observation for press feedback, without interfering with MAUI gesture
/// recognizers ("input platform, output virtual": the visual response stays a virtual view).
/// </summary>
/// <remarks>
/// Down-only by design: on Android the single <c>OnTouchListener</c> slot of any view carrying
/// MAUI gestures belongs to MAUI, so the observation rides a gesture-free descendant — which,
/// by returning false on DOWN, never receives the rest of the touch stream. The press visual
/// is therefore a self-fading PULSE (not a held state), identical on both platforms.
/// </remarks>
internal static partial class ScaffoldPressable
{
    /// <summary>
    /// Invokes <paramref name="onPressed"/> on every raw touch-down within
    /// <paramref name="touchSurface"/> (a gesture-free view; MAUI gestures on its ancestors
    /// keep working). Safe across handler reconnections.
    /// </summary>
    public static void Observe(View touchSurface, Action onPressed)
    {
        touchSurface.HandlerChanged += (_, _) => PlatformAttach(touchSurface, onPressed);

        if (touchSurface.Handler is not null)
        {
            PlatformAttach(touchSurface, onPressed);
        }
    }

    static partial void PlatformAttach(View touchSurface, Action onPressed);
}
