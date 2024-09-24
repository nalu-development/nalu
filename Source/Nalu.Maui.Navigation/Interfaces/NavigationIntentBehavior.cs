namespace Nalu;

/// <summary>
/// Defines how lifecycle events are triggered when a navigation intent is detected.
/// </summary>
public enum NavigationIntentBehavior
{
    /// <summary>
    /// When a navigation intent is detected, only intent-related navigation lifecycle events will be triggered.
    /// </summary>
    /// <example>
    /// In the following example, if <c>MyIntent</c> is detected, the navigation will invoke only <c>OnAppearingAsync(MyIntent)</c>.
    /// <code>
    /// public ValueTask OnEnteringAsync()
    /// public ValueTask OnAppearingAsync(MyIntent)
    /// </code>
    /// </example>
    Strict,

    /// <summary>
    /// When a navigation intent is detected, if the corresponding intent-related navigation lifecycle event is not defined, the navigation will fall through to the generic one.
    /// </summary>
    /// <example>
    /// In the following example, if <c>MyIntent</c> is detected, the navigation will invoke <c>OnEnteringAsync</c> and <c>OnAppearingAsync(MyIntent)</c> in sequence.
    /// <code>
    /// public ValueTask OnEnteringAsync()
    /// public ValueTask OnAppearingAsync(MyIntent)
    /// </code>
    /// </example>
    Fallthrough,
}
