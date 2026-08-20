using CoreGraphics;
using Microsoft.Maui.Platform;
using Nalu.Internals;
using UIKit;

namespace Nalu;

/// <summary>
/// The platform realization of one <see cref="ScaffoldPageHost"/>: the page's view controller
/// with the page's OWN nav bar strip above it. This container — never the page view — is what
/// the presenter mounts, transitions, dims, flies shared elements between and scrubs, so the bar
/// travels with its page through every motion the scaffold performs.
/// </summary>
/// <remarks>
/// <para>
/// Layout discipline: invalidation only marks dirty and requests a layout; the strip is measured
/// and placed, and the page's top inset written, in <see cref="ViewDidLayoutSubviews"/> — never
/// from an invalidation, a dispatch or a timer. The strip terminates MAUI's platform
/// measure-invalidation walk (<see cref="ScaffoldChromeStrip"/>), so a bar that re-measures
/// itself (including the safe-area re-fold it performs in its own layout) lands here as a dirty
/// flag and is re-measured in the next pass.
/// </para>
/// <para>
/// Inset ownership: the page's TOP inset comes from THIS container's strip and nothing else; the
/// bottom (tab bar, keyboard) is pushed in by the root controller through
/// <see cref="SetExternalBottomInset"/>. One writer for
/// <see cref="UIViewController.AdditionalSafeAreaInsets"/> — two would fight every pass.
/// </para>
/// </remarks>
internal sealed class ScaffoldPageHostController : UIViewController
{
    private readonly ScaffoldPageHost _host;
    private readonly IMauiContext _mauiContext;
    private ScaffoldNavBarStrip? _navStrip;
    private nfloat _externalBottomInset;
    private bool _navBarPresented;
    private int _navBarAnimating;

    /// <summary>The page's own view controller (UIKit containment: safe area and appearance propagate).</summary>
    public UIViewController PageController { get; }

    /// <summary>The page's platform view — the surface keyboard Pan translates (the bar stays put).</summary>
    public UIView PageView => PageController.View!;

    /// <summary>The page this container presents.</summary>
    public Page Page => _host.Page;

    public ScaffoldPageHostController(ScaffoldPageHost host, IMauiContext mauiContext)
    {
        _host = host;
        _mauiContext = mauiContext;
        PageController = host.Page.ToUIViewController(mauiContext);
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var container = View!;
        container.BackgroundColor = UIColor.Clear;

        AddChildViewController(PageController);
        var pageView = PageController.View!;
        pageView.Frame = container.Bounds;
        pageView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        container.AddSubview(pageView);
        PageController.DidMoveToParentViewController(this);

        // The strip belongs to the container from birth: mount it before the first layout
        // pass, so the page is laid out with its final top inset rather than jumping into it.
        SyncNavBarAsync(animated: false).FireAndForget(_host.Scaffold.Handler);
    }

    /// <summary>
    /// Brings the strip in line with the page's resolved bar: mounts it on first use, swaps the
    /// hosted bar view VIRTUALLY (the platform host stays, so the strip is told its content
    /// changed) and drives the presented state. Called on every synchronization for this page
    /// and whenever the page's bar-related attached properties change.
    /// </summary>
    public Task SyncNavBarAsync(bool animated)
    {
        var barHost = _host.EnsureNavBarHost();
        var visible = _host.IsNavBarVisible;

        if (barHost is null)
        {
            _navStrip?.RemoveFromSuperview();
            _navStrip = null;
            _navBarPresented = false;
            _host.SetNavBarAttached(false);
            View?.SetNeedsLayout();

            return Task.CompletedTask;
        }

        if (_navStrip is null)
        {
            // The strip is mounted BELOW nothing else here: this container hosts exactly the
            // page and its bar, and the bar always draws over the page.
            var strip = new ScaffoldNavBarStrip(barHost.ToPlatform(_mauiContext));
            _navStrip = strip;
            View!.AddSubview(strip);

            var bounds = View.Bounds;
            strip.Measure(bounds.Width);
            PositionStrip(strip, bounds);

            // A page enters with its bar already at rest: the bar travels WITH the page, so it
            // must not also slide in on its own.
            strip.Transform = visible ? CGAffineTransform.MakeIdentity() : CGAffineTransform.MakeTranslation(0, -strip.BarHeight);
            _navBarPresented = visible;
        }
        else
        {
            // A virtual bar swap keeps the platform host, so nothing else tells the strip its
            // content changed and its cached measure still describes the previous bar.
            _navStrip.InvalidateBarMeasure();
        }

        _host.SetNavBarAttached(visible);

        return SetNavBarPresentedAsync(visible, animated);
    }

    /// <summary>
    /// Slides this page's bar in or out for a SAME-PAGE visibility change, animating the page's
    /// top inset with it. Interruptible: a call while the opposite slide is in flight retargets
    /// it from the current position.
    /// </summary>
    public async Task SetNavBarPresentedAsync(bool presented, bool animated)
    {
        if (_navStrip is not { } strip || (_navBarPresented == presented && _navBarAnimating == 0 && !animated))
        {
            _navBarPresented = presented;
            ApplyPageInsets();

            return;
        }

        _navBarPresented = presented;

        // Bar-INTERNAL layout settles instantly: flushing it inside the animation block would
        // make UIKit interpolate a freshly arranged button from its zero frame.
        UIView.PerformWithoutAnimation(strip.LayoutIfNeeded);

        // Read the target when the transform is WRITTEN, never earlier: a bar swap issued with
        // this call re-measures in the layout pass that runs first.
        CGAffineTransform Target()
            => presented ? CGAffineTransform.MakeIdentity() : CGAffineTransform.MakeTranslation(0, -strip.BarHeight);

        if (!animated)
        {
            strip.Transform = Target();
            ApplyPageInsets();

            return;
        }

        var wasAtRestHidden = _navBarAnimating == 0 && strip.Transform.y0 != 0;
        _navBarAnimating++;

        try
        {
            await ScaffoldViewController.AnimateChromeAsync(
                () =>
                {
                    strip.Transform = Target();
                    ApplyPageInsets();
                    View!.LayoutIfNeeded();
                },
                prepare: () =>
                {
                    // Sliding IN a strip that rested hidden by the PREVIOUS bar's height: start
                    // exactly one current height above the edge, or a shorter bar spends most of
                    // the slide offscreen and pops in at the end.
                    if (presented && wasAtRestHidden)
                    {
                        strip.Transform = CGAffineTransform.MakeTranslation(0, -strip.BarHeight);
                    }
                }
            );
        }
        finally
        {
            _navBarAnimating--;

            // A strip re-measured DURING the slide must rest exactly where its settled state says.
            if (_navBarAnimating == 0 && _navBarPresented == presented)
            {
                strip.Transform = Target();
            }
        }
    }

    /// <summary>
    /// The bottom contribution (tab bar footprint, keyboard, pan compensation) the root
    /// controller computes for the presented page. Stored, not applied directly: this container
    /// composes it with its own top contribution.
    /// </summary>
    public void SetExternalBottomInset(nfloat bottom)
    {
        if (_externalBottomInset == bottom)
        {
            return;
        }

        _externalBottomInset = bottom;
        ApplyPageInsets();
    }

    /// <summary>The strip's footprint ABOVE the system inset — what the page sees as extra top inset.</summary>
    private nfloat TopInsetContribution
        => _navStrip is { } strip && _navBarPresented && _host.WantsNavBarInset
            ? ScaffoldChromeBar.FootprintAboveInset(strip.BarHeight, View?.SafeAreaInsets.Top ?? 0)
            : 0;

    /// <summary>Writes the page's composite additional safe area — the ONE place that does.</summary>
    public void ApplyPageInsets()
    {
        var insets = new UIEdgeInsets(TopInsetContribution, 0, _externalBottomInset, 0);

        // Only on change: writing AdditionalSafeAreaInsets re-dirties the page subtree even when
        // the value is identical, and this runs on every layout pass.
        if (PageController.AdditionalSafeAreaInsets != insets)
        {
            PageController.AdditionalSafeAreaInsets = insets;
        }
    }

    private static void PositionStrip(ScaffoldNavBarStrip strip, CGRect containerBounds)
    {
        var height = strip.BarHeight;
        var bounds = new CGRect(0, 0, containerBounds.Width, height);
        var center = new CGPoint(containerBounds.Width / 2, height / 2);

        // Bounds/Center, never Frame: the strip carries a slide transform, under which the frame
        // is undefined and a write corrupts the running animation.
        if (strip.Bounds == bounds && strip.Center == center)
        {
            return;
        }

        UIView.PerformWithoutAnimation(
            () =>
            {
                strip.Bounds = bounds;
                strip.Center = center;
            }
        );
    }

    // Chrome geometry snaps — never animates implicitly; see ScaffoldViewController.PerformChromeLayout.
    public override void ViewDidLayoutSubviews()
        => ScaffoldViewController.PerformChromeLayout(ViewDidLayoutSubviewsCore);

    private void ViewDidLayoutSubviewsCore()
    {
        base.ViewDidLayoutSubviews();

        var bounds = View!.Bounds;
        var safeTop = View.SafeAreaInsets.Top;
        var changed = bounds != _lastBounds || safeTop != _lastSafeTop;

        if (_navStrip is { } strip)
        {
            if (strip.NeedsMeasure || changed)
            {
                strip.Measure(bounds.Width);
            }

            PositionStrip(strip, bounds);

            // Keep a hidden strip fully offscreen after a size change (rotation) — but never
            // touch the transform while a presentation animation is retargeting it.
            if (_navBarAnimating == 0 && !_navBarPresented)
            {
                strip.Transform = CGAffineTransform.MakeTranslation(0, -strip.BarHeight);
            }
        }

        _lastBounds = bounds;
        _lastSafeTop = safeTop;

        ApplyPageInsets();
    }

    private CGRect _lastBounds;
    private nfloat _lastSafeTop = -1;

    /// <summary>Detaches the page controller from containment (the container is going away).</summary>
    public void TearDown()
    {
        PageController.WillMoveToParentViewController(null);
        PageController.View?.RemoveFromSuperview();
        PageController.RemoveFromParentViewController();
        _navStrip?.RemoveFromSuperview();
        _navStrip = null;
    }
}
