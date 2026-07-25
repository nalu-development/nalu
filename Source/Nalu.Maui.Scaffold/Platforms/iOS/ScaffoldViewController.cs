using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Root view controller of a scaffold-hosted app. Hosts two layers:
/// the content host (a child controller the presenter mounts page controllers onto) and the
/// bottom tab bar strip. The chrome's safe-area contribution (§5.4) is applied PER PAGE
/// controller (each page is laid out with the insets matching its own bar visibility from
/// birth — no cross-page relayout jumps), and bar hide/show animates in sync with page
/// transitions. Overlays (flyout, overflow panel) are added on top by the presenter.
/// </summary>
internal sealed class ScaffoldViewController : UIViewController
{
    private const double _barAnimationDurationSeconds = 0.25;

    private readonly UIViewController _contentHost = new();
    private ScaffoldTabBarStrip? _tabBarStrip;
    private CGRect _lastBounds;
    private nfloat _lastSafeBottom = -1;

    /// <summary>The controller page view controllers are added to (UIKit containment).</summary>
    public UIViewController ContentHost => _contentHost;

    /// <summary>The view page platform views are mounted into (fills the whole controller).</summary>
    public UIView ContentContainer => _contentHost.View!;

    /// <summary>
    /// The current page's controller — the carrier of the bar's
    /// <see cref="UIViewController.AdditionalSafeAreaInsets"/> contribution. Set by the
    /// presenter on every transition; bar animations update its insets in the same animation.
    /// </summary>
    public UIViewController? CurrentPageController { get; set; }

    /// <summary>Whether the current page sees the bar footprint as extra bottom inset.</summary>
    public bool CurrentPageWantsBarInset { get; set; }

    /// <summary>
    /// Bar footprint (points) above the system inset — the page-facing inset contribution.
    /// Zero when no bar is mounted.
    /// </summary>
    public nfloat BarHeight => _tabBarStrip?.BarHeight ?? 0;

    /// <summary>
    /// Full height (points) of the bottom chrome strip measured from the screen's bottom edge:
    /// bar footprint + system bottom inset. Zero when no tab bar is mounted.
    /// </summary>
    public nfloat ChromeBottomFootprint { get; private set; }

    /// <summary>
    /// The bottom chrome strip platform view, when mounted. Overlays that must keep the tab bar
    /// interactive (the overflow panel) insert their fullscreen scrim BELOW this view in z-order.
    /// </summary>
    public UIView? ChromeBottomLayer => _tabBarStrip;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var container = View!;
        AddChildViewController(_contentHost);
        var contentView = _contentHost.View!;
        contentView.Frame = container.Bounds;
        contentView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        container.AddSubview(contentView);
        _contentHost.DidMoveToParentViewController(this);
    }

    /// <summary>
    /// Mounts the tab bar strip (hidden below the screen edge when <paramref name="startHidden"/>)
    /// and measures it synchronously so <see cref="BarHeight"/> is valid immediately —
    /// the presenter needs the footprint BEFORE mounting the target page.
    /// </summary>
    public void MountTabBar(UIView barPlatformView, bool startHidden)
    {
        if (ReferenceEquals(_tabBarStrip?.Bar, barPlatformView))
        {
            return;
        }

        _tabBarStrip?.RemoveFromSuperview();

        var strip = new ScaffoldTabBarStrip(barPlatformView);
        _tabBarStrip = strip;
        View!.AddSubview(strip);

        var bounds = View.Bounds;
        var safeBottom = View.SafeAreaInsets.Bottom;
        strip.Measure(bounds.Width);

        var stripHeight = strip.BarHeight + safeBottom;
        strip.Frame = new CGRect(0, bounds.Height - stripHeight, bounds.Width, stripHeight);
        strip.Transform = startHidden ? CGAffineTransform.MakeTranslation(0, stripHeight) : CGAffineTransform.MakeIdentity();
        ChromeBottomFootprint = startHidden ? 0 : stripHeight;
    }

    /// <summary>Slides the mounted strip into place (no-op when none is mounted or it is already shown).</summary>
    public async Task ShowTabBarAsync(bool animated)
    {
        if (_tabBarStrip is not { } strip)
        {
            return;
        }

        ChromeBottomFootprint = strip.Frame.Height;

        if (animated && !strip.Transform.IsIdentity)
        {
            await UIView.AnimateAsync(_barAnimationDurationSeconds, () =>
            {
                strip.Transform = CGAffineTransform.MakeIdentity();
                ApplyCurrentPageInsets();
                View!.LayoutIfNeeded();
            });
        }
        else
        {
            strip.Transform = CGAffineTransform.MakeIdentity();
            ApplyCurrentPageInsets();
        }
    }

    /// <summary>
    /// Slides the mounted strip below the screen edge and unmounts it (the element tree
    /// reflects presented chrome).
    /// </summary>
    public async Task HideAndUnmountTabBarAsync(bool animated)
    {
        if (_tabBarStrip is not { } strip)
        {
            return;
        }

        ChromeBottomFootprint = 0;

        if (animated)
        {
            await UIView.AnimateAsync(_barAnimationDurationSeconds, () =>
            {
                strip.Transform = CGAffineTransform.MakeTranslation(0, strip.Frame.Height);
                ApplyCurrentPageInsets();
                View!.LayoutIfNeeded();
            });
        }

        strip.RemoveFromSuperview();
        _tabBarStrip = null;
        View!.SetNeedsLayout();
    }

    /// <summary>Applies the current page's bar-inset contribution to its own controller.</summary>
    public void ApplyCurrentPageInsets()
    {
        if (CurrentPageController is { } pageController)
        {
            pageController.AdditionalSafeAreaInsets = CurrentPageWantsBarInset && _tabBarStrip is not null
                ? new UIEdgeInsets(0, 0, BarHeight, 0)
                : UIEdgeInsets.Zero;
        }
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();

        var container = View!;
        var bounds = container.Bounds;
        var safeBottom = container.SafeAreaInsets.Bottom;

        if (_tabBarStrip is { } strip)
        {
            if (strip.NeedsMeasure || bounds != _lastBounds || safeBottom != _lastSafeBottom)
            {
                _lastBounds = bounds;
                _lastSafeBottom = safeBottom;
                strip.Measure(bounds.Width);
            }

            var stripHeight = strip.BarHeight + safeBottom;
            var presented = strip.Transform.IsIdentity;
            strip.Frame = new CGRect(0, bounds.Height - stripHeight, bounds.Width, stripHeight);
            ChromeBottomFootprint = presented ? stripHeight : 0;
        }

        ApplyCurrentPageInsets();
    }
}

/// <summary>
/// Bottom chrome strip hosting the MAUI tab bar platform view: measures it (cached until
/// invalidated — the NaluTabBarContainerView pattern), lays it out above the system inset, and
/// passes touches through everywhere the bar itself is not hit (the floating pill's side margins
/// must not swallow page taps).
/// </summary>
internal sealed class ScaffoldTabBarStrip : UIView
{
    public UIView Bar { get; }

    internal bool NeedsMeasure { get; private set; } = true;

    internal nfloat BarHeight { get; private set; }

    public ScaffoldTabBarStrip(UIView bar)
    {
        Bar = bar;
        BackgroundColor = UIColor.Clear;
        (bar.Superview as UIView)?.WillRemoveSubview(bar);
        bar.RemoveFromSuperview();
        AddSubview(bar);
    }

    internal void Measure(nfloat width)
    {
        BarHeight = Bar.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        NeedsMeasure = false;
    }

    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();
        NeedsMeasure = true;
        Superview?.SetNeedsLayout();
    }

    public override void SafeAreaInsetsDidChange()
    {
        base.SafeAreaInsetsDidChange();
        NeedsMeasure = true;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        Bar.Frame = new CGRect(0, 0, Bounds.Width, BarHeight);
    }

    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        var view = base.HitTest(point, uievent);

        // Only the bar's actual content consumes touches; the strip itself is transparent glass.
        return ReferenceEquals(view, this) ? null : view;
    }
}
