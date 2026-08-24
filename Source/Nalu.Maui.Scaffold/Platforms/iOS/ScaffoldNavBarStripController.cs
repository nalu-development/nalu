using UIKit;

namespace Nalu;

/// <summary>
/// Owns ONE page's nav bar strip as a view controller, so the bar's subtree can carry a safe area
/// of its own: the LEADING inset that keeps its buttons and title clear of the system window
/// controls (see <see cref="ScaffoldWindowControls"/>).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UIViewController.AdditionalSafeAreaInsets"/> applies to one controller's subtree,
/// which is exactly the reach wanted here: the inset lands on the BAR — the default bar and any
/// custom bar consume the container safe area already, so both shift their content while still
/// painting their background across the full strip — and NOT on the page, a sibling child
/// controller that must not move.
/// </para>
/// <para>
/// The controller's view IS the strip: containment adds a controller, not a view level, so the
/// strip's measure-invalidation contract with MAUI is untouched.
/// </para>
/// </remarks>
internal sealed class ScaffoldNavBarStripController(ScaffoldNavBarStrip strip) : UIViewController
{
    public ScaffoldNavBarStrip Strip { get; } = strip;

    /// <inheritdoc />
    public override void LoadView() => View = Strip;

    /// <summary>
    /// Re-reads the leading inset the window controls impose. Called from the container's layout
    /// pass: the geometry it depends on — the window's size on screen, the strip's own position —
    /// only changes with a layout.
    /// </summary>
    /// <returns>Whether the inset changed, in which case the bar needs re-measuring.</returns>
    public bool UpdateWindowControlsInset()
    {
        // What the strip would inherit without this controller's own contribution.
        var inherited = Strip.SafeAreaInsets.Left - AdditionalSafeAreaInsets.Left;
        var inset = ScaffoldWindowControls.LeadingInsetFor(Strip, inherited);

        // Writing AdditionalSafeAreaInsets re-dirties the whole subtree, so only on a real change.
        if (Math.Abs(AdditionalSafeAreaInsets.Left - inset) < 0.5)
        {
            return false;
        }

        AdditionalSafeAreaInsets = new UIEdgeInsets(0, inset, 0, 0);

        return true;
    }
}
