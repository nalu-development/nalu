using CoreFoundation;
using CoreGraphics;
using Foundation;
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
/// What the host DOES have to guarantee is that the answer is current. MAUI folds the consumed
/// inset into the size in <c>CrossPlatformMeasure</c>, but only detects an inset change while the
/// bar lays out (<c>ValidateSafeArea</c> runs in <c>LayoutSubviews</c>); a changed fold, like any
/// measure change in the bar subtree, then climbs the platform hierarchy
/// (<c>InvalidateAncestorsMeasures</c>) — the scaffold keeps every layer between the bar and the
/// strip non-Fixed so the walk reaches the strip, which implements
/// <see cref="IPlatformMeasureInvalidationController"/>: mark dirty, request layout on itself and
/// its superview, and the controller re-measures the strip in ITS layout pass. Pure platform
/// layout discipline — no Controls-level <c>MeasureInvalidated</c> event, no inline
/// <c>LayoutIfNeeded</c> drains, no deferred settle.
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
}

/// <summary>
/// Base of the chrome strips hosting a MAUI bar view: a plain native superview where the bar's
/// measure invalidations (its own or propagated from its subtree, including the safe-area re-fold
/// MAUI performs in the bar's <c>LayoutSubviews</c>) terminate
/// (<see cref="IPlatformMeasureInvalidationController"/>), only marking the strip dirty and
/// requesting a layout; the controller re-measures the strip (<see cref="Measure"/>) in its own
/// layout pass. The bar keeps measuring and arranging itself in its own <c>LayoutSubviews</c>;
/// only a strip hosting a DIRECT MAUI bar view also declares itself a cross-platform layout
/// backing (see <see cref="CrossPlatformLayout"/>). The bar FILLS the strip.
/// </summary>
internal abstract class ScaffoldChromeStrip : UIView, IPlatformMeasureInvalidationController, ICrossPlatformLayoutBacking
{
    private bool _invalidateOnWindow;

    public UIView Bar { get; }

    internal bool NeedsMeasure { get; private set; } = true;

    /// <summary>The bar's height, INCLUDING any safe-area padding it chose to consume.</summary>
    internal nfloat BarHeight { get; private set; }

    protected ScaffoldChromeStrip(UIView bar, bool handlesBarMeasure)
    {
        Bar = bar;
        BackgroundColor = UIColor.Clear;
        CrossPlatformLayout = handlesBarMeasure ? new BarLayout(this) : null;
        bar.Superview?.WillRemoveSubview(bar);
        bar.RemoveFromSuperview();
        AddSubview(bar);
    }

    /// <summary>
    /// Non-null only for a strip whose bar is a DIRECT MAUI view (no MAUI-owned host chain between):
    /// MAUI reports the bar's safe-area re-fold upward (<c>InvalidateAncestorsMeasures</c> from its
    /// <c>LayoutSubviews</c>) only when its superview is a cross-platform layout backing, so the
    /// strip declares itself as such — the bar's measure/arrange still happen in the controller's
    /// pass, this object is that contract in MAUI terms. A bar mounted through a MAUI host (nav bar)
    /// already has a backing superview inside the chain and the strip stays a plain native view.
    /// </summary>
    public ICrossPlatformLayout? CrossPlatformLayout { get; set; }

    private sealed class BarLayout(ScaffoldChromeStrip strip) : ICrossPlatformLayout
    {
        public Size CrossPlatformMeasure(double widthConstraint, double heightConstraint)
            => strip.Bar.SizeThatFits(new CGSize(widthConstraint, heightConstraint)).ToSize();

        public Size CrossPlatformArrange(Rect bounds)
        {
            strip.Bar.Frame = bounds.ToCGRect();

            return bounds.Size;
        }
    }

    /// <summary>Measures the bar for the given width (called by the controller in its layout pass).</summary>
    internal void Measure(nfloat width)
    {
        BarHeight = ScaffoldChromeBar.MeasureHeight(Bar, width);
        NeedsMeasure = false;
    }

    /// <summary>
    /// The hosted bar's CONTENT changed (a virtual bar swap keeps this strip's platform view, so no
    /// platform invalidation is raised): mark dirty and request the pass.
    /// </summary>
    internal void InvalidateBarMeasure() => MarkMeasureDirty();

    private void MarkMeasureDirty()
    {
        NeedsMeasure = true;
        SetNeedsLayout();
        Superview?.SetNeedsLayout();
    }

    /// <returns><c>false</c>: propagation stops here — the strip owns what happens above it.</returns>
    bool IPlatformMeasureInvalidationController.InvalidateMeasure(bool isPropagating)
    {
        MarkMeasureDirty();

        return false;
    }

    void IPlatformMeasureInvalidationController.InvalidateAncestorsMeasuresWhenMovedToWindow()
        => _invalidateOnWindow = true;

    public override void MovedToWindow()
    {
        base.MovedToWindow();

        if (_invalidateOnWindow && Window is not null)
        {
            _invalidateOnWindow = false;
            ((IPlatformMeasureInvalidationController)this).InvalidateMeasure(isPropagating: true);
        }
    }

    public override void SafeAreaInsetsDidChange()
    {
        base.SafeAreaInsetsDidChange();

        // The bar's cached measure predates these insets: the bar re-folds them in its own layout
        // and reports back through InvalidateMeasure; marking here covers strips whose bar consumes nothing.
        MarkMeasureDirty();
    }

    // The ENTIRE pass is unanimated — see ScaffoldViewController.PerformChromeLayout: this can
    // run inside the window rotation animation block, where every frame write would otherwise
    // stack an additive animation per pass.
    public override void LayoutSubviews()
        => ScaffoldViewController.PerformChromeLayout(LayoutSubviewsCore);

    private void LayoutSubviewsCore()
    {
        base.LayoutSubviews();

        // The bar FILLS the strip, system-inset region included: custom bars can paint under the
        // system bar (their SafeAreaEdges decides any inner padding). Its safe-area state validates
        // in ITS layout, which follows this one; a changed fold comes back through InvalidateMeasure.
        Bar.Frame = Bounds;
    }

    public override UIView? HitTest(CGPoint point, UIEvent? uievent)
    {
        var view = base.HitTest(point, uievent);

        // Only the bar's actual content consumes touches; the strip itself is transparent glass.
        return ReferenceEquals(view, this) ? null : view;
    }
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
    /// The current page's soft-keyboard policy (resolved by the presenter, live: the attached
    /// value may change while the page is presented).
    /// </summary>
    public Func<ScaffoldKeyboardMode>? CurrentPageKeyboardMode { get; set; }

    /// <summary>
    /// Whether a presented sheet/popup OWNS the keyboard (set by the presenter). The keyboard inset
    /// goes to ONE surface: the topmost sheet or popup when one is presented, the page otherwise —
    /// so a page never resizes/pans under an overlay's keyboard.
    /// </summary>
    public Func<bool>? OverlayOwnsKeyboard { get; set; }

    /// <summary>The keyboard overlap the CURRENT PAGE reacts to: 0 while an overlay owns the keyboard.</summary>
    private double PageKeyboardOverlap => OverlayOwnsKeyboard?.Invoke() == true ? 0 : (double)_lastKeyboardOverlap;

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

        // Keyboard tracking through UIKit's own guide (iOS 15+): a hidden, input-transparent
        // view pinned between the guide's top edge and the container's bottom edge. Whenever the
        // keyboard's frame changes, the guide's constraints change and the tracker is re-framed
        // — its height IS the keyboard overlap, and its geometry change (which UIKit applies
        // INSIDE the keyboard animation) is what syncs the overlays, so they travel with the
        // keyboard. No notifications, no window-coordinate math.
        var tracker = new KeyboardTrackerView(OnKeyboardTrackerGeometryChanged);
        container.AddSubview(tracker);
        _keyboardTracker = tracker;
        ObserveEditingFocus();

        var keyboardGuide = container.KeyboardLayoutGuide;

        NSLayoutConstraint.ActivateConstraints(
        [
            tracker.TopAnchor.ConstraintEqualTo(keyboardGuide.TopAnchor),
            tracker.BottomAnchor.ConstraintEqualTo(container.BottomAnchor),
            tracker.LeadingAnchor.ConstraintEqualTo(container.LeadingAnchor),
            tracker.TrailingAnchor.ConstraintEqualTo(container.TrailingAnchor)
        ]);
    }

    private KeyboardTrackerView? _keyboardTracker;
    private NSObject? _textFieldBeganEditingToken;
    private NSObject? _textViewBeganEditingToken;

    /// <summary>
    /// A Pan-mode surface follows the FOCUSED input, and focus can move without the keyboard
    /// moving (tab to the next field): the begin-editing notifications re-raise
    /// <see cref="KeyboardOverlapChanged"/> so presenters re-place their Pan surfaces. (These are
    /// text-input notifications, not keyboard geometry — the geometry stays with the guide.)
    /// </summary>
    private void ObserveEditingFocus()
    {
        _textFieldBeganEditingToken = UITextField.Notifications.ObserveTextDidBeginEditing((_, _) => OnEditingFocusChanged());
        _textViewBeganEditingToken = UITextView.Notifications.ObserveTextDidBeginEditing((_, _) => OnEditingFocusChanged());
        _textViewChangedToken = UITextView.Notifications.ObserveTextDidChange((_, _) => OnEditingTextChanged());
    }

    private void OnEditingFocusChanged()
    {
        if (_lastKeyboardOverlap > 0)
        {
            ApplyCurrentPageKeyboard();
            KeyboardOverlapChanged?.Invoke();
        }
    }

    /// <summary>
    /// The caret of a multi-line text view moves as the user types (an auto-sizing editor grows
    /// under it): a Pan surface must follow it, or the caret walks under the keyboard. The text view
    /// re-lays out (and MAUI re-measures an auto-sizing editor) after the notification, so the
    /// re-pan waits one runloop turn and is animated — a short glide instead of a per-line snap.
    /// </summary>
    private void OnEditingTextChanged()
    {
        if (_lastKeyboardOverlap <= 0 || _caretFollowScheduled)
        {
            return;
        }

        _caretFollowScheduled = true;

        CoreFoundation.DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            _caretFollowScheduled = false;

            if (_lastKeyboardOverlap <= 0)
            {
                return;
            }

            UIView.Animate(0.15, 0, UIViewAnimationOptions.CurveEaseOut | UIViewAnimationOptions.BeginFromCurrentState, () =>
            {
                ApplyCurrentPageKeyboard();
                KeyboardOverlapChanged?.Invoke();
                View?.LayoutIfNeeded();
            }, () => { });
        });
    }

    private bool _caretFollowScheduled;
    private NSObject? _textViewChangedToken;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textFieldBeganEditingToken?.Dispose();
            _textViewBeganEditingToken?.Dispose();
            _textViewChangedToken?.Dispose();
            _textFieldBeganEditingToken = null;
            _textViewBeganEditingToken = null;
            _textViewChangedToken = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// The height (points) of the docked soft keyboard's overlap with the container, measured from
    /// the container's bottom edge; 0 while the keyboard is hidden or undocked. Read LIVE from the
    /// tracker (an overlay presented while the keyboard is already up must see it).
    /// </summary>
    public nfloat KeyboardOverlap => ReadKeyboardOverlap();

    private nfloat _lastKeyboardOverlap;

    /// <summary>
    /// Raised from the keyboard tracker's geometry change when <see cref="KeyboardOverlap"/> changed
    /// (and when the editing focus moves while the keyboard is up — Pan surfaces follow the focused
    /// input). Unlike <see cref="WindowGeometryChanged"/> the pass that raises it is NOT unanimated:
    /// it runs inside UIKit's keyboard animation, and overlays re-placed against the keyboard travel
    /// with it.
    /// </summary>
    public Action? KeyboardOverlapChanged { get; set; }

    private bool _keyboardRecheckScheduled;

    private void OnKeyboardTrackerGeometryChanged()
    {
        EvaluateKeyboardOverlap();

        // The geometry change and the responder change are not simultaneous: dismissing the
        // keyboard re-frames the guide while the text input is STILL first responder (observed:
        // the guide's dismissed rest arrives before the resign completes), and the resign itself
        // moves no frame. Re-evaluate once the current turn is over, so the settled responder
        // state is what gates the settled geometry.
        if (!_keyboardRecheckScheduled)
        {
            _keyboardRecheckScheduled = true;

            DispatchQueue.MainQueue.DispatchAsync(() =>
            {
                _keyboardRecheckScheduled = false;
                EvaluateKeyboardOverlap();
            });
        }
    }

    private void EvaluateKeyboardOverlap()
    {
        var overlap = ReadKeyboardOverlap();

        if (overlap != _lastKeyboardOverlap)
        {
            _lastKeyboardOverlap = overlap;
            ApplyCurrentPageKeyboard();
            KeyboardOverlapChanged?.Invoke();
        }
    }

    /// <summary>
    /// The current page's reaction to the keyboard: Resize re-applies its insets (see
    /// <see cref="ApplyCurrentPageInsets"/>); Pan slides the page's view by the least that keeps
    /// the focused input above the keyboard (never more than the keyboard's overlap); None leaves it
    /// alone. Runs inside the keyboard animation (KVO) — the page travels with it.
    /// </summary>
    private void ApplyCurrentPageKeyboard()
    {
        if (CurrentPageController?.View is not { } pageView)
        {
            return;
        }

        var mode = CurrentPageKeyboardMode?.Invoke() ?? ScaffoldKeyboardMode.Resize;
        var overlap = PageKeyboardOverlap;

        double pan = 0;

        if (mode == ScaffoldKeyboardMode.Pan && overlap > 0 && View is { } container)
        {
            var keyboardTop = (double)container.Bounds.Height - overlap;
            var focused = ScaffoldFocusedInput.BottomIn(pageView);
            var needed = focused is { } focusedBottom ? focusedBottom + ScaffoldOverlayGeometry.PanGap - keyboardTop : overlap;
            pan = Math.Clamp(needed, 0, overlap);
        }

        _currentPagePan = pan;
        pageView.Transform = pan > 0 ? CGAffineTransform.MakeTranslation(0, (nfloat)(-pan)) : CGAffineTransform.MakeIdentity();

        ApplyCurrentPageInsets();
    }

    private double _currentPagePan;

    /// <summary>Re-applies the current page's keyboard reaction (the presenter calls it after a page swap).</summary>
    public void RefreshCurrentPageKeyboard() => ApplyCurrentPageKeyboard();

    /// <summary>
    /// The guide is a keyboard only while something is being edited. When the keyboard is dismissed
    /// the guide rests on the bottom safe area — but with a text input accessory view (MAUI's
    /// "Done" band) it has been observed to settle at the ACCESSORY's height instead (44pt on iOS
    /// 26), which is not a keyboard either: without a first responder there is nothing docked, so
    /// the overlap is 0 whatever the guide reads. With a first responder (a hardware keyboard
    /// showing only the accessory band, for instance) the guide is trusted as is.
    /// </summary>
    private nfloat ReadKeyboardOverlap()
    {
        if (_keyboardTracker is not { } tracker || View is not { } container)
        {
            return 0;
        }

        var height = tracker.Frame.Height;

        if (height <= container.SafeAreaInsets.Bottom + 0.5f)
        {
            return 0;
        }

        return container.Window is { } window && HasFirstResponder(window) ? height : 0;
    }

    private static bool HasFirstResponder(UIView view)
    {
        if (view.IsFirstResponder)
        {
            return true;
        }

        foreach (var subview in view.Subviews)
        {
            if (HasFirstResponder(subview))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The keyboard tracker: hidden, input-transparent, its only job is to report changes of its
    /// own geometry (its frame follows the keyboard layout guide it is constrained to). Observed
    /// through KVO on <c>bounds</c>: UIKit applies constraint results through its
    /// internal geometry setters, so neither the managed <c>Frame</c>/<c>Bounds</c> setter
    /// overrides nor <c>LayoutSubviews</c> (a leaf view) see them — KVO does.
    /// </summary>
    private sealed class KeyboardTrackerView : UIView
    {
        private readonly Action _geometryChanged;
        private readonly IDisposable _boundsObserver;

        public KeyboardTrackerView(Action geometryChanged)
        {
            _geometryChanged = geometryChanged;
            Hidden = true;
            UserInteractionEnabled = false;
            TranslatesAutoresizingMaskIntoConstraints = false;

            // Only bounds: the tracker spans from the guide's top edge to the container's bottom
            // edge, so its HEIGHT is the overlap — the position carries no extra information.
            _boundsObserver = AddObserver("bounds", NSKeyValueObservingOptions.New, _ => _geometryChanged());
        }

        public override UIView? HitTest(CGPoint point, UIEvent? uievent) => null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _boundsObserver.Dispose();
            }

            base.Dispose(disposing);
        }
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

        PositionChromeStrip(
            strip,
            new CGRect(0, 0, containerBounds.Width, stripHeight),
            new CGPoint(containerBounds.Width / 2, stripHeight / 2)
        );
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
        => PositionChromeStrip(
            strip,
            new CGRect(0, 0, containerBounds.Width, stripHeight),
            new CGPoint(containerBounds.Width / 2, containerBounds.Height - (stripHeight / 2))
        );

    /// <summary>
    /// Writes a strip's resting geometry, and ONLY when it actually moved.
    /// </summary>
    /// <remarks>
    /// This runs from every layout pass, and rotation runs its passes inside UIKit's rotation
    /// animation block — where each write to an animatable property enrolls in the running
    /// animation. The strips animate ADDITIVELY (see SetTabBarPresentedAsync), so UIKit walks the
    /// layer's accumulated animation list on every enrolment: re-writing an unchanged Center each
    /// pass grows that list until the walk eats the main thread, observed as a freeze deep in
    /// _shouldAnimateAdditivelyForKey. Writing only real changes keeps the list short, and
    /// PerformWithoutAnimation keeps resting geometry out of the animation entirely — the slide
    /// itself rides on Transform, not on these.
    /// </remarks>
    private static void PositionChromeStrip(UIView strip, CGRect bounds, CGPoint center)
    {
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

    /// <summary>
    /// Runs chrome layout work with implicit animations OFF.
    /// </summary>
    /// <remarks>
    /// Chrome layout passes can run INSIDE an animation block: the window ROTATION animation drives
    /// them through LayoutIfNeeded (in-app geometry updates ride the same rotation controller), and
    /// the chrome's own show/hide does so deliberately. Inside such a block every write to an
    /// animatable property enrolls an ADDITIVE animation — and a strip settle re-arranges the whole
    /// MAUI bar subtree, so each pass stacks one more animation on every bar layer. UIKit walks the
    /// accumulated stack on each enrolment (_shouldAnimateAdditivelyForKey), so repeated passes turn
    /// the walk quadratic until the main thread never returns from the drain: the freeze observed on
    /// device rotation with a scaffold on screen, and absent from any scaffold-free page.
    /// <para>
    /// The rule this encodes: chrome MOTION rides exclusively on Transform (the slide animations);
    /// chrome GEOMETRY — strip frames, bar frames, bar-subtree arrangement — always snaps. A bar
    /// re-placed for a new window shape is not a movement the user should watch.
    /// </para>
    /// </remarks>
    internal static void PerformChromeLayout(Action layout) => UIView.PerformWithoutAnimation(layout);

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

    /// <summary>
    /// Applies the current page's chrome inset contributions to its own controller — and, under
    /// <see cref="ScaffoldKeyboardMode.Resize"/>, the soft keyboard: the keyboard becomes the page's
    /// bottom safe-area inset (it covers the bar and the system inset, so it REPLACES their
    /// contribution rather than adding to it), and the page lays out above it exactly as it does
    /// above the home indicator.
    /// </summary>
    public void ApplyCurrentPageInsets()
    {
        if (CurrentPageController is { } pageController)
        {
            var bottom = CurrentPageWantsBarInset && _barPresented && _tabBarStrip is not null ? BarHeight : 0;
            var top = CurrentPageWantsNavBarInset ? NavBarInsetContribution : 0;
            var systemBottom = View?.SafeAreaInsets.Bottom ?? 0;

            if (CurrentPageKeyboardMode?.Invoke() == ScaffoldKeyboardMode.Resize && PageKeyboardOverlap is var keyboard && keyboard > 0)
            {
                bottom = (nfloat)Math.Max((double)bottom, keyboard - systemBottom);
            }

            // Pan: the page's view is translated up by the pan, and UIKit derives a view's safe
            // area from where it IS — the bottom inset shrinks by the pan (the top one stays: UIKit
            // does not extend a safe area past a view's own edge, and additional insets clamp at
            // 0), which would reflow the content instead of sliding it. Compensate, so the page's
            // safe area is exactly what it was at rest.
            if (_currentPagePan > 0)
            {
                bottom += (nfloat)Math.Min(_currentPagePan, (double)systemBottom);
            }

            var insets = new UIEdgeInsets(top, 0, bottom, 0);

            // Only on change: writing AdditionalSafeAreaInsets re-dirties the page subtree even
            // when the value is identical, and this runs on EVERY host layout pass.
            if (pageController.AdditionalSafeAreaInsets != insets)
            {
                pageController.AdditionalSafeAreaInsets = insets;
            }
        }
    }

    // Chrome geometry snaps — never animates implicitly; see PerformChromeLayout.
    public override void ViewDidLayoutSubviews()
        => PerformChromeLayout(ViewDidLayoutSubviewsCore);

    private void ViewDidLayoutSubviewsCore()
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
/// Bottom chrome strip hosting the MAUI tab bar platform view: the bar FILLS the strip flush to
/// the screen's bottom edge and owns the bottom system inset (SafeAreaEdges semantics — the
/// default template's Auto-row root keeps its pill above the inset).
/// </summary>
internal sealed class ScaffoldTabBarStrip(UIView bar) : ScaffoldChromeStrip(bar, handlesBarMeasure: true);

/// <summary>
/// Top chrome strip hosting the MAUI nav bar platform view: the bar FILLS the strip (its
/// background extends under the status bar) and consumes the safe-area padding itself
/// (SafeAreaEdges). Measurement is NORMALIZED to the content height by the controller: once
/// positioned at the top the bar's measure includes the status padding it consumed, so it is
/// subtracted back (the NaluShellItemRenderer net10 pattern) — the controller adds the system
/// inset deterministically.
/// </summary>
internal sealed class ScaffoldNavBarStrip(UIView bar) : ScaffoldChromeStrip(bar, handlesBarMeasure: false);
