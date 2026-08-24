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
    private readonly UIView _panel = panel;

    /// <inheritdoc />
    public override void LoadView() => View = _panel;

    /// <summary>
    /// Gives the panel back: UIKit allows a view exactly ONE controller, and a flyout's content
    /// view is presented again on the next open. Without this the next presentation throws
    /// <c>UIViewControllerHierarchyInconsistency</c>, because leaving containment does not clear
    /// the association — only letting go of the view does.
    /// </summary>
    public void ReleaseView()
    {
        AdditionalSafeAreaInsets = UIEdgeInsets.Zero;
        View = null;
    }

    /// <summary>Re-reads the top inset the window controls impose; called from the layout pass.</summary>
    public void UpdateWindowControlsInset()
    {
        var inherited = _panel.SafeAreaInsets.Top - AdditionalSafeAreaInsets.Top;
        var inset = ScaffoldWindowControls.TopInsetFor(_panel, inherited);

        if (Math.Abs(AdditionalSafeAreaInsets.Top - inset) < 0.5)
        {
            return;
        }

        AdditionalSafeAreaInsets = new UIEdgeInsets(inset, 0, 0, 0);
    }
}
