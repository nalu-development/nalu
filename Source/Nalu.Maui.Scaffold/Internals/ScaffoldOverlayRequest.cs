namespace Nalu;

/// <summary>What an overlay entry is — selects z-slot, geometry and enter/exit animation.</summary>
internal enum ScaffoldOverlayKind
{
    /// <summary>An edge drawer (top layer, slides from the side). Single instance.</summary>
    Flyout,

    /// <summary>The tab bar panel (below the tab bar strip in z-order, rises above the bottom chrome). Single instance.</summary>
    TabBarPanel
}

/// <summary>
/// One overlay presentation: the §5.6 primitive generalized to a STACK. The scaffold builds
/// requests (resolving scrims, single-instance policies and lifecycle hooks); the presenters
/// realize them (platform mounting, z-slots, geometry) and animate at the virtual view layer.
/// A request instance is the identity of its presentation — close it by reference.
/// </summary>
internal sealed class ScaffoldOverlayRequest
{
    /// <summary>The overlay kind.</summary>
    public required ScaffoldOverlayKind Kind { get; init; }

    /// <summary>The presented content view.</summary>
    public required View Content { get; init; }

    /// <summary>The scrim brush (never null; may be fully transparent — the scrim always blocks input).</summary>
    public required Brush Scrim { get; init; }

    /// <summary>Whether tapping the scrim closes this entry.</summary>
    public bool CloseOnScrimTap { get; init; } = true;

    /// <summary>
    /// Whether a system back gesture closes this entry while it is topmost. When false, back is
    /// consumed without closing.
    /// </summary>
    public bool CloseOnBack { get; init; } = true;

    /// <summary>Whether the content's handlers are disconnected when the entry closes (single-use content).</summary>
    public bool DisconnectContentOnClose { get; init; }

    /// <summary>The drawer side; meaningful for <see cref="ScaffoldOverlayKind.Flyout"/> only.</summary>
    public ScaffoldFlyoutSide FlyoutSide { get; init; }

    /// <summary>The automation id of the scrim view (UI-test hook for scrim taps).</summary>
    public string? ScrimAutomationId { get; init; }

    /// <summary>
    /// Invoked EXACTLY ONCE when the entry leaves the presentation (any close path), or
    /// immediately when presenting fails. Owner-side lifecycle: logical-child detach,
    /// state flags, handle completion.
    /// </summary>
    public Action? Cleanup { get; set; }

    /// <summary>Builds the scrim view realized behind the content (fades in/out with the entry).</summary>
    public View CreateScrimView()
        => new Border
        {
            StrokeThickness = 0,
            Background = Scrim,
            Opacity = 0,
            AutomationId = ScrimAutomationId
        };
}

/// <summary>
/// The shared enter/exit choreography of overlay entries, expressed on MAUI views (platform
/// mappers translate to native transforms — presenters must never animate the same views at the
/// platform layer, and always frame platform views BEFORE these transforms apply).
/// </summary>
internal static class ScaffoldOverlayAnimations
{
    private const uint _duration = 250;

    /// <summary>Prepares the content's animated properties for entry (called after platform mounting, before <see cref="EnterAsync"/>).</summary>
    /// <param name="request">The entry.</param>
    /// <param name="flyoutOffscreenTranslation">The physical offscreen X translation of a flyout (±width).</param>
    public static void PrepareEnter(ScaffoldOverlayRequest request, double flyoutOffscreenTranslation)
    {
        var content = request.Content;

        switch (request.Kind)
        {
            case ScaffoldOverlayKind.Flyout:
                content.TranslationX = flyoutOffscreenTranslation;

                break;

            case ScaffoldOverlayKind.TabBarPanel:
                content.Opacity = 0;
                content.TranslationY = 24;

                break;
        }
    }

    /// <summary>Animates the entry in (scrim fade + kind-specific content motion).</summary>
    public static Task EnterAsync(ScaffoldOverlayRequest request, View scrimView)
        => Task.WhenAll(
            scrimView.FadeTo(1, _duration),
            request.Kind switch
            {
                ScaffoldOverlayKind.Flyout => request.Content.TranslateTo(0, 0, _duration, Easing.CubicOut),
                _ => Task.WhenAll(
                    request.Content.FadeTo(1, _duration),
                    request.Content.TranslateTo(0, 0, _duration, Easing.CubicOut)
                )
            }
        );

    /// <summary>Animates the entry out (mirror of <see cref="EnterAsync"/>).</summary>
    public static Task ExitAsync(ScaffoldOverlayRequest request, View scrimView, double flyoutOffscreenTranslation)
        => Task.WhenAll(
            scrimView.FadeTo(0, _duration),
            request.Kind switch
            {
                ScaffoldOverlayKind.Flyout => request.Content.TranslateTo(flyoutOffscreenTranslation, 0, _duration, Easing.CubicIn),
                _ => Task.WhenAll(
                    request.Content.FadeTo(0, _duration),
                    request.Content.TranslateTo(0, 24, _duration, Easing.CubicIn)
                )
            }
        );

    /// <summary>
    /// Clears the animated properties after unmounting: overlay content can be REUSED (flyout
    /// drawers, custom panels) and must not carry stale transforms into its next home.
    /// </summary>
    public static void ResetContent(View content)
    {
        content.TranslationX = 0;
        content.TranslationY = 0;
        content.Opacity = 1;
    }
}
