using UIKit;

namespace Nalu;

/// <summary>
/// Owns a LEFT-edge flyout panel as a view controller, so the drawer's content can carry the TOP
/// inset that keeps it clear of the system window controls (see <see cref="ScaffoldWindowControls"/>).
/// </summary>
/// <remarks>
/// <para>
/// Top rather than leading: a drawer runs the window's full height, so insetting its leading edge
/// would waste the panel's width all the way down for controls that only cover its top corner.
/// Pushing the content down clears them and costs one band.
/// </para>
/// <para>
/// Only the panel on the physical LEFT gets one — the controls are drawn there whatever the
/// layout direction, so an end-side (or RTL start-side) drawer never reaches them. The
/// controller's view IS the panel: containment adds no view level, and the presenter keeps
/// framing, translating and animating the same view it always did.
/// </para>
/// </remarks>
internal sealed class ScaffoldFlyoutPanelController(UIView panel) : UIViewController
{
    private UIView? _panel = panel;

    /// <summary>
    /// Hands the panel out ONCE. After <see cref="ReleaseView"/> a lazy <c>view</c> access
    /// (UIKit loads on demand) gets a throwaway view instead: re-claiming the panel would
    /// silently tie it back to a retired controller, and the next presentation would throw.
    /// </summary>
    public override void LoadView() => View = _panel ?? new UIView();

    /// <summary>
    /// Gives the panel back: UIKit allows a view exactly ONE controller, and a flyout's content
    /// view is presented again on the next open. Without this the next presentation throws
    /// <c>UIViewControllerHierarchyInconsistency</c>, because leaving containment does not clear
    /// the association — only letting go of the view does.
    /// </summary>
    public void ReleaseView()
    {
        _panel = null;
        AdditionalSafeAreaInsets = UIEdgeInsets.Zero;
        View = null;
    }

    /// <summary>
    /// Frees the panel from a retired controller that still holds it — a presentation that never
    /// reached the close routine (an owner callback that threw, a presenter torn down with the
    /// drawer open). The controller of a view is its next responder; a released panel (or one
    /// never hosted) answers its superview instead.
    /// </summary>
    public static void ReleaseStaleOwner(UIView panel)
    {
        if (panel.NextResponder is not ScaffoldFlyoutPanelController stale)
        {
            return;
        }

        stale.WillMoveToParentViewController(null);
        stale.RemoveFromParentViewController();
        stale.ReleaseView();
    }

    /// <summary>Re-reads the top inset the window controls impose; called from the layout pass.</summary>
    public void UpdateWindowControlsInset()
    {
        if (_panel is not { } panel)
        {
            return;
        }

        var inherited = panel.SafeAreaInsets.Top - AdditionalSafeAreaInsets.Top;
        var inset = ScaffoldWindowControls.TopInsetFor(panel, inherited);

        if (Math.Abs(AdditionalSafeAreaInsets.Top - inset) < 0.5)
        {
            return;
        }

        AdditionalSafeAreaInsets = new UIEdgeInsets(inset, 0, 0, 0);
    }
}
