using CoreFoundation;
using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// Measurement shared by the chrome strips hosting a MAUI bar view (nav bar, tab bar).
/// </summary>
/// <remarks>
/// <para>
/// The bar decides how much room it needs, INCLUDING whatever system inset it chooses to consume
/// through its own safe-area behavior — the strip never second-guesses that declaration. It cannot:
/// the behavior comes from <c>SafeAreaEdges</c> on layouts, from <c>ISafeAreaView.IgnoreSafeArea</c>
/// on plain views (a Nalu <c>ViewBox</c> lands here and consumes by default), and from MAUI's own
/// per-type defaults, whose meaning is still moving upstream (dotnet/maui#34872). Reading any of
/// that from the host would encode an interpretation that breaks the moment MAUI changes it.
/// </para>
/// <para>
/// What the host DOES have to guarantee is that the answer is current: MAUI folds the consumed
/// inset into the size in <c>CrossPlatformMeasure</c>, but only arms that fold while laying out
/// (<c>ValidateSafeArea</c> runs in <c>LayoutSubviews</c>; <c>SizeThatFits</c> replies from cache),
/// and it propagates the resulting invalidation to the host only when the bar's superview is a
/// cross-platform layout backing — which a native strip is not. Without settling the layout first,
/// a bar that consumes the inset on its ROOT keeps the height it reported before placement, when
/// no inset had reached it yet, and nothing ever asks again.
/// </para>
/// </remarks>
internal static class ScaffoldChromeBar
{
    /// <summary>The bar's height, including whatever system inset it chose to consume.</summary>
    internal static nfloat MeasureHeight(UIView bar, nfloat width)
        => bar.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;

    /// <summary>
    /// A strip's page-facing contribution: its part ABOVE the system inset it extends under. The
    /// page already receives that inset from the system safe area, so only the surplus is added —
    /// and never a negative value, which an edge-to-edge bar SHORTER than the inset would
    /// otherwise produce, pulling page content under the system bar.
    /// </summary>
    internal static nfloat FootprintAboveInset(nfloat measured, nfloat systemInset)
        => (nfloat)Math.Max(0, measured - systemInset);

    /// <summary>
    /// Lays the bar out at the frame it has just been given, so its safe-area state is current,
    /// and drops the cached measure taken before the insets reached it.
    /// </summary>
    /// <remarks>
    /// MAUI folds the consumed system inset into the bar's size in <c>CrossPlatformMeasure</c>, but
    /// arms that fold only while the bar lays out (<c>ValidateSafeArea</c> runs in
    /// <c>LayoutSubviews</c>; <c>SizeThatFits</c> replies from cache), and it reports the resulting
    /// change to the host only when the bar's superview is a cross-platform layout backing — which
    /// a native strip is not. Running the bar's layout here, AFTER its final frame is assigned, is
    /// what makes the following measure meaningful.
    /// </remarks>
    internal static void SettleBarAtCurrentFrame(UIView bar)
    {
        bar.SetNeedsLayout();
        bar.LayoutIfNeeded();

        if (bar is MauiView { View: { } crossPlatformView })
        {
            crossPlatformView.InvalidateMeasure();
        }
        else
        {
            (bar as IPlatformMeasureInvalidationController)?.InvalidateMeasure();
        }
    }
}

/// <summary>
/// Bounds the strips' settle-then-remeasure cycle, which is a feedback loop by construction: the
/// settle invalidates the bar's measure, that invalidation travels back up through the strip, and
/// the strip answers by dirtying its host — from inside a layout pass.
/// </summary>
/// <remarks>
/// Unbounded, the cycle can wedge the main thread outright. Chrome animations lay the tree out
/// through <c>LayoutIfNeeded</c> inside an animation block, and UIKit drains every dirty view
/// before that call returns: a cycle that re-dirties the host on each pass never lets the drain
/// finish, and the app freezes with the strip's <c>LayoutSubviews</c> on the stack. A bar whose
/// measured height keeps changing across passes — templated cells realizing, an image source
/// resolving — is enough to sustain it.
/// <para>
/// Two bounds, both of which leave a genuine settle intact. A settle is skipped when the geometry
/// it would run for is the one it already ran for, since that settle is provably a no-op; and at
/// most one settle happens per runloop turn, so any cycle that survives the first bound advances
/// one pass per turn instead of spinning inside a single drain. A request that arrives while the
/// latch is closed is not lost — it is re-raised on the next turn.
/// </para>
/// </remarks>
internal sealed class ScaffoldBarSettleGuard(UIView strip)
{
    private bool _needed;
    private bool _settling;
    private bool _latched;
    private bool _hasSettled;
    private CGRect _settledFrame;
    private UIEdgeInsets _settledInsets;

    /// <summary>
    /// Records that the bar's measure no longer describes it. Invalidations raised BY a settle are
    /// ignored: they are its own echo, not a new reason to run another one.
    /// </summary>
    internal void Invalidate()
    {
        if (!_settling)
        {
            _needed = true;
        }
    }

    /// <summary>
    /// Settles the bar if it needs it, at the frame it now has.
    /// </summary>
    /// <returns><c>true</c> when a settle ran, and so the host must re-measure.</returns>
    internal bool SettleIfNeeded(UIView bar)
    {
        if (!_needed)
        {
            return false;
        }

        var frame = bar.Frame;
        var insets = strip.SafeAreaInsets;

        if (_hasSettled && frame == _settledFrame && SameInsets(insets, _settledInsets))
        {
            // Same geometry as the last settle: re-running it would produce the same answer.
            _needed = false;

            return false;
        }

        if (_latched)
        {
            // Already settled this turn. Keep the request and re-raise it on the next one.
            return false;
        }

        _needed = false;
        _latched = true;
        _settling = true;

        try
        {
            ScaffoldChromeBar.SettleBarAtCurrentFrame(bar);
        }
        finally
        {
            _settling = false;
        }

        _hasSettled = true;
        _settledFrame = frame;
        _settledInsets = insets;

        DispatchQueue.MainQueue.DispatchAsync(
            () =>
            {
                _latched = false;

                if (_needed)
                {
                    strip.SetNeedsLayout();
                }
            }
        );

        return true;
    }

    private static bool SameInsets(UIEdgeInsets left, UIEdgeInsets right)
        => left.Top == right.Top
           && left.Bottom == right.Bottom
           && left.Left == right.Left
           && left.Right == right.Right;
}

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
    private ScaffoldNavBarStrip? _navBarStrip;
    private CGRect _lastBounds;
    private nfloat _lastSafeBottom = -1;
    private nfloat _lastSafeTop = -1;
    private bool _barPresented;
    private int _barAnimating;
    private bool _navBarPresented;
    private int _navBarAnimating;

    /// <summary>
    /// The scaffold page hosted by this controller. MAUI's PageHandler-driven appearing
    /// plumbing does not run for a custom root handler, so the controller forwards its own
    /// UIKit appearance callbacks into the page's MAUI events (§10) — parity with
    /// PageHandler-hosted pages (backgrounding/foregrounding stays window-level, as for
    /// every MAUI page).
    /// </summary>
    public Scaffold? Scaffold { get; set; }

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

    /// <summary>Whether the current page sees the nav bar footprint as extra top inset.</summary>
    public bool CurrentPageWantsNavBarInset { get; set; }

    /// <summary>
    /// Bar footprint (points) above the system inset — the page-facing inset contribution.
    /// The strip's measured height INCLUDES any bottom inset the bar consumed (SafeAreaEdges),
    /// and the page already receives the system inset from the system safe area, so the
    /// contribution is the measured height minus the system inset. Zero when no bar is mounted.
    /// </summary>
    public nfloat BarHeight => _tabBarStrip is { } strip
        ? ScaffoldChromeBar.FootprintAboveInset(strip.BarHeight, View?.SafeAreaInsets.Bottom ?? 0)
        : 0;

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

    /// <summary>
    /// Raised after the container changed shape (rotation, split view). Presented overlays compute
    /// their geometry from the window of the moment they were shown, so they need to hear about it.
    /// </summary>
    public Action? WindowGeometryChanged { get; set; }

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

    private bool? _lightSystemBars;

    /// <summary>
    /// The scaffold-resolved status bar icon style (see <see cref="Nalu.Internals.ScaffoldSystemBars"/>);
    /// UIKit animates the change with a cross-fade. Until first resolution the style is
    /// <see cref="UIStatusBarStyle.Default"/> (theme-following).
    /// </summary>
    public override UIStatusBarStyle PreferredStatusBarStyle()
        => _lightSystemBars switch
        {
            true => UIStatusBarStyle.LightContent,
            // Default follows the theme on iOS 13+ — the correct dark-icon fallback pre-13.
            false when OperatingSystem.IsIOSVersionAtLeast(13) => UIStatusBarStyle.DarkContent,
            _ => UIStatusBarStyle.Default
        };

    /// <summary>Applies the resolved system-bar icon style (animated).</summary>
    public void SetLightSystemBars(bool light)
    {
        if (_lightSystemBars == light)
        {
            return;
        }

        _lightSystemBars = light;
        UIView.Animate(0.25, SetNeedsStatusBarAppearanceUpdate);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        (Scaffold as IPageController)?.SendAppearing();
    }

    public override void ViewDidDisappear(bool animated)
    {
        base.ViewDidDisappear(animated);
        (Scaffold as IPageController)?.SendDisappearing();
    }

    /// <summary>
    /// Mounts the tab bar strip (hidden below the screen edge when <paramref name="startHidden"/>)
    /// and measures it synchronously so <see cref="BarHeight"/> is valid immediately —
    /// the presenter needs the footprint BEFORE mounting the target page. The strip then STAYS
    /// mounted across visibility changes (hidden = translated offscreen): nothing is torn down
    /// mid-animation and re-showing is instant.
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
        strip.Measure(bounds.Width);

        // The strip is exactly the bar's measured height: the BAR owns the bottom inset
        // (SafeAreaEdges semantics, nav-strip parity) — a consuming bar measures inset
        // included, an edge-to-edge bar measures content only. The first (pre-placement)
        // measure has no inset contribution yet; the layout pass re-measures once placed.
        var stripHeight = strip.BarHeight;
        PositionStrip(strip, bounds, stripHeight);
        strip.Transform = startHidden ? CGAffineTransform.MakeTranslation(0, stripHeight) : CGAffineTransform.MakeIdentity();
        _barPresented = !startHidden;
        ChromeBottomFootprint = startHidden ? 0 : stripHeight;
    }

    /// <summary>Removes the strip entirely (area change / bar view replacement / teardown).</summary>
    public void UnmountTabBar()
    {
        _tabBarStrip?.RemoveFromSuperview();
        _tabBarStrip = null;
        _barPresented = false;
        ChromeBottomFootprint = 0;
        View!.SetNeedsLayout();
    }

    /// <summary>
    /// Mounts the nav bar strip at the top edge (measured synchronously — the presenter needs
    /// the footprint BEFORE mounting the target page). Mounted BELOW the tab bar strip in
    /// z-order so behind-chrome overlay scrims dim the nav bar while keeping the tab bar
    /// interactive. The strip stays mounted across visibility changes (hidden = translated
    /// above the screen edge).
    /// </summary>
    public void MountNavBar(UIView barPlatformView, bool startHidden)
    {
        if (ReferenceEquals(_navBarStrip?.Bar, barPlatformView))
        {
            return;
        }

        _navBarStrip?.RemoveFromSuperview();

        var strip = new ScaffoldNavBarStrip(barPlatformView);
        _navBarStrip = strip;

        if (_tabBarStrip is { } tabBarStrip)
        {
            View!.InsertSubviewBelow(strip, tabBarStrip);
        }
        else
        {
            View!.AddSubview(strip);
        }

        var bounds = View.Bounds;
        strip.Measure(bounds.Width);
        PositionNavStrip(strip, bounds);
        strip.Transform = startHidden ? CGAffineTransform.MakeTranslation(0, -NavStripHeight(strip)) : CGAffineTransform.MakeIdentity();
        _navBarPresented = !startHidden;
    }

    /// <summary>
    /// Re-measures the nav bar strip after a VIRTUAL bar swap (the platform host stays mounted, so
    /// nothing else tells the strip its content changed).
    /// </summary>
    public void InvalidateNavBarMeasure() => _navBarStrip?.InvalidateBarMeasure();

    /// <summary>Removes the nav bar strip entirely (nav bar view swap / teardown).</summary>
    public void UnmountNavBar()
    {
        _navBarStrip?.RemoveFromSuperview();
        _navBarStrip = null;
        _navBarPresented = false;
        View!.SetNeedsLayout();
    }

    /// <summary>Full strip height: whatever the bar measured, system inset region included.</summary>
    private nfloat NavStripHeight(ScaffoldNavBarStrip strip) => strip.BarHeight;

    private void PositionNavStrip(ScaffoldNavBarStrip strip, CGRect containerBounds)
    {
        var stripHeight = NavStripHeight(strip);
        strip.Bounds = new CGRect(0, 0, containerBounds.Width, stripHeight);
        strip.Center = new CGPoint(containerBounds.Width / 2, stripHeight / 2);
    }

    /// <summary>
    /// Slides the nav bar strip in or out — interruptible, same retargeting model as the
    /// tab bar strip.
    /// </summary>
    public async Task SetNavBarPresentedAsync(bool presented, bool animated)
    {
        if (_navBarStrip is not { } strip)
        {
            return;
        }

        _navBarPresented = presented;

        // Bar-INTERNAL layout changes (e.g. the back button appearing via the context binding)
        // settle instantly: flushing them inside the animation block below would make UIKit
        // interpolate the button from its never-arranged zero frame — a nonsense fly-in.
        // Only the strip transform and the page inset relayout are meant to animate.
        UIView.PerformWithoutAnimation(strip.LayoutIfNeeded);

        var targetTransform = presented
            ? CGAffineTransform.MakeIdentity()
            : CGAffineTransform.MakeTranslation(0, -NavStripHeight(strip));

        if (!animated)
        {
            strip.Transform = targetTransform;
            ApplyCurrentPageInsets();

            return;
        }

        _navBarAnimating++;

        try
        {
            await AnimateChromeAsync(
                () =>
                {
                    strip.Transform = targetTransform;
                    ApplyCurrentPageInsets();
                    View!.LayoutIfNeeded();
                }
            );
        }
        finally
        {
            _navBarAnimating--;
        }
    }

    /// <summary>
    /// Runs a chrome slide, completing when UIKit reports it finished.
    /// Deliberately NOT <c>UIView.AnimateNotifyAsync</c>: starting the animation inline can block
    /// and never return (iOS 18.x, observed while a cross-area switch mounted a strip), and a
    /// NAVIGATION awaits this — a call that never returns wedges navigation for good, leaving the
    /// incoming page unpresented and its Appearing unraised. Posting to the main queue starts the
    /// animation on the NEXT turn instead, and its completion handler resolves the task.
    /// </summary>
    private static Task AnimateChromeAsync(Action animation)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        DispatchQueue.MainQueue.DispatchAsync(
            () => UIView.AnimateNotify(
                _barAnimationDurationSeconds,
                0,
                UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.AllowUserInteraction,
                animation,
                _ => completion.TrySetResult()
            )
        );

        return completion.Task;
    }

    /// <summary>
    /// Positions the strip via Bounds/Center — transform-safe: setting Frame under an active
    /// slide transform corrupts the geometry and snaps the hide/show animation
    /// (layout passes run INSIDE the animation block via LayoutIfNeeded).
    /// </summary>
    private static void PositionStrip(ScaffoldTabBarStrip strip, CGRect containerBounds, nfloat stripHeight)
    {
        strip.Bounds = new CGRect(0, 0, containerBounds.Width, stripHeight);
        strip.Center = new CGPoint(containerBounds.Width / 2, containerBounds.Height - (stripHeight / 2));
    }

    /// <summary>
    /// Slides the mounted strip in or out. INTERRUPTIBLE: a call while the opposite animation
    /// is in flight retargets it from the current position (additive UIKit animations +
    /// BeginFromCurrentState) — rapid visibility toggles reverse smoothly instead of queueing.
    /// </summary>
    public async Task SetTabBarPresentedAsync(bool presented, bool animated)
    {
        if (_tabBarStrip is not { } strip)
        {
            return;
        }

        _barPresented = presented;
        ChromeBottomFootprint = presented ? strip.Bounds.Height : 0;

        // Same rationale as the nav bar strip: bar-internal layout settles without animation.
        UIView.PerformWithoutAnimation(strip.LayoutIfNeeded);

        var targetTransform = presented
            ? CGAffineTransform.MakeIdentity()
            : CGAffineTransform.MakeTranslation(0, strip.Bounds.Height);

        if (!animated)
        {
            strip.Transform = targetTransform;
            ApplyCurrentPageInsets();

            return;
        }

        _barAnimating++;

        try
        {
            await AnimateChromeAsync(
                () =>
                {
                    strip.Transform = targetTransform;
                    ApplyCurrentPageInsets();
                    View!.LayoutIfNeeded();
                }
            );
        }
        finally
        {
            _barAnimating--;
        }
    }

    /// <summary>The nav bar's top inset contribution above the system inset: the bar's content height.</summary>
    private nfloat NavBarInsetContribution
        => _navBarStrip is { } strip && _navBarPresented
            ? ScaffoldChromeBar.FootprintAboveInset(strip.BarHeight, View!.SafeAreaInsets.Top)
            : 0;

    /// <summary>Applies the current page's chrome inset contributions to its own controller.</summary>
    public void ApplyCurrentPageInsets()
    {
        if (CurrentPageController is { } pageController)
        {
            var bottom = CurrentPageWantsBarInset && _barPresented && _tabBarStrip is not null ? BarHeight : 0;
            var top = CurrentPageWantsNavBarInset ? NavBarInsetContribution : 0;
            pageController.AdditionalSafeAreaInsets = new UIEdgeInsets(top, 0, bottom, 0);
        }
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();

        var container = View!;
        var bounds = container.Bounds;
        var safeBottom = container.SafeAreaInsets.Bottom;

        var boundsOrInsetsChanged = bounds != _lastBounds || safeBottom != _lastSafeBottom || container.SafeAreaInsets.Top != _lastSafeTop;

        if (_tabBarStrip is { } strip)
        {
            if (strip.NeedsMeasure || boundsOrInsetsChanged)
            {
                strip.Measure(bounds.Width);
            }

            var stripHeight = strip.BarHeight;
            PositionStrip(strip, bounds, stripHeight);
            ChromeBottomFootprint = _barPresented ? stripHeight : 0;

            // Keep a hidden strip fully offscreen after size changes (rotation) — but never
            // touch the transform while a presentation animation is retargeting it.
            if (_barAnimating == 0 && !_barPresented)
            {
                strip.Transform = CGAffineTransform.MakeTranslation(0, stripHeight);
            }
        }

        if (_navBarStrip is { } navStrip)
        {
            if (navStrip.NeedsMeasure || boundsOrInsetsChanged)
            {
                navStrip.Measure(bounds.Width);
            }

            PositionNavStrip(navStrip, bounds);

            if (_navBarAnimating == 0 && !_navBarPresented)
            {
                navStrip.Transform = CGAffineTransform.MakeTranslation(0, -NavStripHeight(navStrip));
            }
        }

        if (boundsOrInsetsChanged)
        {
            WindowGeometryChanged?.Invoke();
        }

        _lastBounds = bounds;
        _lastSafeBottom = safeBottom;
        _lastSafeTop = container.SafeAreaInsets.Top;

        ApplyCurrentPageInsets();
    }
}

/// <summary>
/// Bottom chrome strip hosting the MAUI tab bar platform view: measures it (cached until
/// invalidated — the NaluTabBarContainerView pattern) and lets the bar FILL the strip flush to
/// the screen's bottom edge. The BAR owns the bottom system inset (SafeAreaEdges semantics,
/// symmetric with the nav strip): a consuming bar measures inset-included, an edge-to-edge bar
/// measures content-only. Touches pass through everywhere the bar itself is not hit (the
/// floating pill's side margins must not swallow page taps).
/// </summary>
internal sealed class ScaffoldTabBarStrip : UIView
{
    public UIView Bar { get; }

    internal bool NeedsMeasure { get; private set; } = true;

    private readonly ScaffoldBarSettleGuard _settle;

    internal nfloat BarHeight { get; private set; }

    public ScaffoldTabBarStrip(UIView bar)
    {
        Bar = bar;
        _settle = new ScaffoldBarSettleGuard(this);
        BackgroundColor = UIColor.Clear;
        (bar.Superview as UIView)?.WillRemoveSubview(bar);
        bar.RemoveFromSuperview();
        AddSubview(bar);
    }

    internal void Measure(nfloat width)
    {
        BarHeight = ScaffoldChromeBar.MeasureHeight(Bar, width);
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

        // The bar's cached measure predates these insets. It can only re-fold them once it lays
        // out at its final frame, which happens in OUR layout pass — flag it for there.
        _settle.Invalidate();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        // The bar FILLS the strip, system-inset region included: custom bars can paint under
        // the home indicator (their SafeAreaEdges decides any inner padding), while the
        // default template's Auto-row root keeps its pill above the inset (Auto rows
        // top-align at their measured height).
        Bar.Frame = Bounds;

        // The bar now has its final frame, so laying it out validates its safe area and re-folds
        // the inset. Dirty the host only if that CHANGED the bar's answer: a settle that leaves the
        // height where it was gives the host nothing to do, and asking anyway is what let this pass
        // feed itself. A bar that consumes nothing (SafeAreaEdges.None) answers the same every
        // time, so it now costs one settle and stops.
        if (_settle.SettleIfNeeded(Bar))
        {
            var settled = ScaffoldChromeBar.MeasureHeight(Bar, Bounds.Width);

            if (settled != BarHeight)
            {
                BarHeight = settled;
                NeedsMeasure = true;
                Superview?.SetNeedsLayout();
            }
        }
    }

    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        var view = base.HitTest(point, uievent);

        // Only the bar's actual content consumes touches; the strip itself is transparent glass.
        return ReferenceEquals(view, this) ? null : view;
    }
}

/// <summary>
/// Top chrome strip hosting the MAUI nav bar platform view. Unlike the tab bar strip, the bar
/// view FILLS the strip (its background extends under the status bar) and consumes the
/// safe-area padding itself (SafeAreaEdges). Measurement is NORMALIZED to the content height:
/// once positioned at the top the bar's measure includes the status padding it consumed, so it
/// is subtracted back (the NaluShellItemRenderer net10 pattern) — the controller adds the
/// system inset deterministically.
/// </summary>
internal sealed class ScaffoldNavBarStrip : UIView
{
    public UIView Bar { get; }

    internal bool NeedsMeasure { get; private set; } = true;

    private readonly ScaffoldBarSettleGuard _settle;

    /// <summary>The bar's height, INCLUDING any safe-area padding it chose to consume.</summary>
    internal nfloat BarHeight { get; private set; }

    public ScaffoldNavBarStrip(UIView bar)
    {
        Bar = bar;
        _settle = new ScaffoldBarSettleGuard(this);
        BackgroundColor = UIColor.Clear;
        (bar.Superview as UIView)?.WillRemoveSubview(bar);
        bar.RemoveFromSuperview();
        AddSubview(bar);
    }

    internal void Measure(nfloat width)
    {
        BarHeight = ScaffoldChromeBar.MeasureHeight(Bar, width);
        NeedsMeasure = false;
    }

    /// <summary>
    /// The hosted bar's CONTENT changed — a virtual bar swap, which keeps this strip's platform
    /// view and so raises no inset callback. The measure still describes the previous bar, and the
    /// incoming one has not laid out yet, so it goes through the same settle-then-measure path.
    /// </summary>
    internal void InvalidateBarMeasure()
    {
        _settle.Invalidate();
        SetNeedsLayout();
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

        // The bar's cached measure predates these insets. It can only re-fold them once it lays
        // out at its final frame, which happens in OUR layout pass — flag it for there.
        _settle.Invalidate();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        Bar.Frame = Bounds;

        // Same settle-then-remeasure contract as the tab bar strip, and the same two bounds on it:
        // see ScaffoldTabBarStrip.LayoutSubviews and ScaffoldBarSettleGuard.
        if (_settle.SettleIfNeeded(Bar))
        {
            var settled = ScaffoldChromeBar.MeasureHeight(Bar, Bounds.Width);

            if (settled != BarHeight)
            {
                BarHeight = settled;
                NeedsMeasure = true;
                Superview?.SetNeedsLayout();
            }
        }
    }

    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        var view = base.HitTest(point, uievent);

        // Only the bar's actual content consumes touches; the strip itself is transparent glass.
        return ReferenceEquals(view, this) ? null : view;
    }
}
