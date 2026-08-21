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
    private ScaffoldPageHost? _host;
    private readonly IMauiContext _mauiContext;
    private ScaffoldNavBarStrip? _navStrip;
    private ScaffoldNavBarStripController? _navStripController;
    private nfloat _externalBottomInset;
    private bool _navBarPresented;
    private int _navBarAnimating;

    /// <summary>The page's own view controller (UIKit containment: safe area and appearance propagate).</summary>
    public UIViewController PageController { get; }

    /// <summary>The page's platform view — the surface keyboard Pan translates (the bar stays put).</summary>
    public UIView PageView => PageController.View!;

    /// <summary>The page this container presents.</summary>
    public Page Page => Host.Page;

    /// <summary>
    /// The page host, until this container is torn down. Nothing reaches it afterwards: every
    /// caller is either the presenter (which drops the container at the same moment) or a UIKit
    /// callback on a view that has been unmounted.
    /// </summary>
    private ScaffoldPageHost Host => _host!;

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
        SyncNavBarAsync(animated: false).FireAndForget(Host.Scaffold.Handler);
    }

    /// <summary>
    /// Brings the strip in line with the page's resolved bar: mounts it on first use, swaps the
    /// hosted bar view VIRTUALLY (the platform host stays, so the strip is told its content
    /// changed) and drives the presented state. Called on every synchronization for this page
    /// and whenever the page's bar-related attached properties change.
    /// </summary>
    public Task SyncNavBarAsync(bool animated)
    {
        var visible = Host.IsNavBarVisible;

        // A page that shows no bar gets NO strip and no bar view at all. Mounting one and
        // relying on a translation to hide it cannot work: at mount the bar has not been
        // arranged, so its height is unknown, and a strip translated by an unknown height is a
        // VISIBLE strip for the length of the transition.
        if (!visible && _navStrip is null)
        {
            _navBarPresented = false;
            Host.SetNavBarAttached(false);
            ApplyPageInsets();

            return Task.CompletedTask;
        }

        var barHost = Host.EnsureNavBarHost();

        if (barHost is null)
        {
            DetachNavStrip();
            _navBarPresented = false;
            Host.SetNavBarAttached(false);
            View?.SetNeedsLayout();

            return Task.CompletedTask;
        }

        if (_navStrip is null)
        {
            // The strip is mounted BELOW nothing else here: this container hosts exactly the
            // page and its bar, and the bar always draws over the page.
            var strip = new ScaffoldNavBarStrip(barHost.ToPlatform(_mauiContext));
            _navStrip = strip;

            // The strip is owned by a controller so it can carry its own safe area: on iPadOS 26
            // the system windowing controls sit over the window's top-leading corner, and only a
            // controller's AdditionalSafeAreaInsets can push the BAR's content clear of them
            // without moving the page, which is this container's other child controller.
            var stripController = new ScaffoldNavBarStripController(strip);
            _navStripController = stripController;
            AddChildViewController(stripController);
            View!.AddSubview(stripController.View!);
            stripController.DidMoveToParentViewController(this);

            // A page enters with its bar already at rest: the bar travels WITH the page, so it
            // must not also slide in on its own.
            _navBarPresented = visible;

            // Hidden is the resting state of a bar that is not presented — NOT a translation by
            // its height, which is unknown until the bar has been arranged.
            strip.Hidden = !visible;
        }
        else
        {
            // A virtual bar swap keeps the platform host, so nothing else tells the strip its
            // content changed and its cached measure still describes the previous bar.
            _navStrip.InvalidateBarMeasure();
        }

        Host.SetNavBarAttached(visible);

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
            strip.Hidden = !presented;
            strip.Transform = Target();
            ApplyPageInsets();

            return;
        }

        // Sliding needs the strip on screen for the whole flight, whichever way it is going.
        strip.Hidden = false;

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
                strip.Hidden = !presented;
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
        => _navStrip is { } strip && _navBarPresented && Host.WantsNavBarInset
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
            // The windowing-controls inset depends on where this strip sits in the window and on
            // whether the window is one: both are layout facts, so they are read here. A change
            // re-dirties the bar, which re-measures in this same pass.
            var insetChanged = _navStripController?.UpdateWindowControlsInset() ?? false;

            if (strip.NeedsMeasure || changed || insetChanged)
            {
                strip.Measure(bounds.Width);
            }

            PositionStrip(strip, bounds);

            // Keep a hidden strip fully offscreen after a size change (rotation) — but never
            // touch the transform while a presentation animation is retargeting it.
            if (_navBarAnimating == 0)
            {
                strip.Hidden = !_navBarPresented;

                if (!_navBarPresented)
                {
                    strip.Transform = CGAffineTransform.MakeTranslation(0, -strip.BarHeight);
                }
            }
        }

        _lastBounds = bounds;
        _lastSafeTop = safeTop;

        ApplyPageInsets();
    }

    /// <summary>Unmounts the strip and its controller from containment (both go away together).</summary>
    private void DetachNavStrip()
    {
        if (_navStripController is { } controller)
        {
            controller.WillMoveToParentViewController(null);
            controller.View?.RemoveFromSuperview();
            controller.RemoveFromParentViewController();
            _navStripController = null;
        }
        else
        {
            _navStrip?.RemoveFromSuperview();
        }

        _navStrip = null;
    }

    private CGRect _lastBounds;
    private nfloat _lastSafeTop = -1;

    /// <summary>Detaches the page controller from containment (the container is going away).</summary>
    public void TearDown()
    {
        PageController.WillMoveToParentViewController(null);
        PageController.View?.RemoveFromSuperview();
        PageController.RemoveFromParentViewController();
        DetachNavStrip();

        // Drop the managed reference chain — host -> page -> page model — and do NOT rely on
        // Dispose to do it. This is a managed UIViewController subclass: disposing it does not
        // clear its fields, and the object itself can outlive the call, because the GC bridge
        // keeps a managed peer alive for as long as anything native still references it. A
        // container that keeps holding its host therefore keeps a whole dead screen alive. This
        // was a measured leak (ScaffoldNavigationTests asserts Leaked:0 after every test), not a
        // precaution: teardown provably ran, the host was provably disposed, and the page model
        // survived anyway until this line existed.
        _host = null;
    }
}
