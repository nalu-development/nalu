using System.ComponentModel;
using CoreGraphics;
using Microsoft.Maui.Platform;
using Nalu.Internals;
using UIKit;

namespace Nalu;

/// <summary>
/// iOS presenter: hosts the visible page as a child UIViewController of the scaffold's content
/// host (UIKit containment — safe area and appearance callbacks propagate), synchronizing to the
/// stack model with a minimal slide transition, and owns the chrome (tab bar strip + §5.6
/// overlay layer). Single-visible-page policy: covered pages are unmounted and remounted on
/// reveal. The full transition engine (shared elements, interactive pop) arrives with P2.
/// </summary>
internal sealed class ScaffoldPresenter(Scaffold scaffold) : IScaffoldPresenter
{
    private const double _transitionDurationSeconds = 0.25;
    private const double _overflowGap = 8;

    private Page? _currentPage;
    private ScaffoldRoot? _currentRoot;
    private UIViewController? _currentController;
    private ScaffoldTabBar? _currentTabBarArea;
    private View? _currentBarView;
    private View? _currentNavBarView;

    private ScaffoldEdgePanRecognizer? _edgeGesture;
    private InteractivePopState? _interactivePop;
    private InteractivePopState? _popHandoff;
    private bool _syncInFlight;

    /// <summary>A live edge-swipe pop: the scrub session plus everything needed to settle it.</summary>
    private sealed record InteractivePopState(
        Page TopPage,
        Page BelowPage,
        UIView BelowView,
        UIView TopView,
        double Width,
        ScaffoldPopAnimationSession Session);

    private UIView? _overlayScrim;
    private UIView? _overlayPanel;
    private View? _overlayContent;
    private ScaffoldOverlayPlacement _overlayPlacement;
    private Action? _overlayCleanup;

    public bool HasOverlay => _overlayPanel is not null;

    private enum ScaffoldOverlayPlacement
    {
        FlyoutStart,
        FlyoutEnd,
        AboveBottomChrome
    }

    public async Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
    {
        if (scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, MauiContext: { } mauiContext })
        {
            return;
        }

        EnsureEdgeGesture(controller);

        // A navigation arriving while a finger is still scrubbing (programmatic push, tab
        // selection) invalidates the preview: cancel the recognizer — its Cancelled callback
        // reverses the session and unmounts the peek before we proceed.
        if (_interactivePop is not null && _popHandoff is null && _edgeGesture is { } gesture)
        {
            gesture.Enabled = false;
            gesture.Enabled = true;
        }

        _syncInFlight = true;

        try
        {
            await SynchronizeCoreAsync(root, hint, controller, mauiContext);
        }
        finally
        {
            _syncInFlight = false;
        }
    }

    private async Task SynchronizeCoreAsync(ScaffoldRoot root, ScaffoldPresentationHint hint, ScaffoldViewController controller, IMauiContext mauiContext)
    {
        // The presented root is the gesture's source of truth for the stack (self-contained
        // presenter state — the engine proxy is not reliably readable at touch time).
        _currentRoot = root;

        // Navigation dismisses any open overlay (flyout, overflow panel).
        await CloseOverlayAsync();

        var stack = root.NavigationStack;
        var targetPage = stack.PushedPages.Count > 0 ? stack.PushedPages[^1].Page : stack.RootPage;

        if (targetPage is null)
        {
            return;
        }

        var tabBarArea = root.Parent as ScaffoldTabBar;
        var barVisible = tabBarArea is not null && Scaffold.ComputeTabBarVisible(root, targetPage);
        var navBarView = scaffold.ResolveNavBarView(targetPage);
        var navBarVisible = navBarView is not null && Scaffold.GetIsNavBarVisible(targetPage);
        var animated = hint != ScaffoldPresentationHint.None;

        // The context must carry the target page's state before the bar (or its bindings) mount.
        scaffold.NavBarContext.Update(root, targetPage);

        // Chrome and page animate CONCURRENTLY: an Auto-hiding bar slides away while the pushed
        // page slides in (and back in sync on pop) — no sequential two-phase motion.
        // Nav bar first: its strip must sit BELOW the tab bar strip in z-order.
        var navChromeTask = UpdateNavBarChromeAsync(controller, mauiContext, navBarView, navBarVisible, animated);
        var chromeTask = UpdateTabBarChromeAsync(controller, mauiContext, tabBarArea, barVisible, animated);

        var pageTask = ReferenceEquals(targetPage, _currentPage)
            ? Task.CompletedTask
            : TransitionToPageAsync(controller, mauiContext, targetPage, hint, barVisible, navBarVisible);

        await Task.WhenAll(navChromeTask, chromeTask, pageTask);
    }

    /// <summary>
    /// Interactive pop (left-edge swipe): scrubs the SAME single-animator pop choreography a
    /// programmatic pop plays (page slide + shared-element flights), with the page below
    /// peek-mounted presentation-only. Release either reverses everything, or settles the
    /// visuals forward and THEN dispatches the pop through the engine (guards and lifecycle run
    /// normally) — the resulting sync adopts the settled state without re-animating.
    /// Guarded pages (ILeavingGuard) get no preview: the gesture never begins on them.
    /// </summary>
    private void EnsureEdgeGesture(ScaffoldViewController controller)
    {
        if (_edgeGesture is not null)
        {
            return;
        }

        var recognizer = new ScaffoldEdgePanRecognizer(OnEdgePan);

        recognizer.ShouldBegin = r =>
        {
            // Edge + direction gate (a plain pan sees every touch — WE decide what qualifies):
            // must start in the leading-edge zone and move forward. The direction cone is
            // deliberately WIDE (~63° per side): ShouldBegin fires after ~10pt of movement,
            // where finger wobble dominates — a strict 45° cone makes the gesture feel like it
            // only accepts perfectly horizontal swipes. Genuinely vertical drags still fail
            // here and fall through to the scroll view.
            var pan = (ScaffoldEdgePanRecognizer)r;
            var translation = pan.TranslationInView(pan.View);

            return pan.StartedAtLeadingEdge
                && translation.X > 0
                && Math.Abs((double)translation.Y) <= (double)translation.X * 2
                && CanBeginInteractivePop();
        };

        // NO failure requirements — measured deadlock (iOS 26, simulator AND device): making
        // scroll pans wait for this recognizer chains with the scroll view's own
        // delaysTouchesBegan gate, which then withholds OUR touches until the gesture ends
        // (TouchesBegan flushed at the release position → edge/direction gates see garbage).
        // The plain race is safe: a vertical-only scroll view does not engage on horizontal
        // edge drags, and vertical drags fail our direction gate and scroll normally.

        // The recognizer lives on the ROOT content container (stable across page transitions):
        // touches on any descendant page reach it through the normal ancestor gesture chain,
        // and CanBeginInteractivePop gates when it may engage.
        _edgeGesture = recognizer;
        controller.ContentContainer.AddGestureRecognizer(recognizer);
    }

    private bool CanBeginInteractivePop()
    {
        var canBegin = !_syncInFlight
                       && !HasOverlay
                       && _interactivePop is null
                       && _popHandoff is null
                       && _currentController?.View is not null;

        if (!canBegin)
        {
            return false;
        }
        
        if (_currentRoot?.NavigationStack is { PushedPages.Count: > 0 } stack &&
            stack.PushedPages[^1].Page is { BindingContext: not ILeavingGuard } topPage &&
            Scaffold.GetPageMode(topPage) == ScaffoldPageMode.Default)
        {
            var topPageMatches = ReferenceEquals(topPage, _currentPage);

            return topPageMatches;
        }

        return false;
    }

    private void OnEdgePan(UIPanGestureRecognizer recognizer)
    {
        switch (recognizer.State)
        {
            case UIGestureRecognizerState.Began:
                BeginInteractivePop();

                break;

            case UIGestureRecognizerState.Changed when _interactivePop is { } state:
                state.Session.SetProgress((double)recognizer.TranslationInView(recognizer.View).X / state.Width);

                break;

            case UIGestureRecognizerState.Ended:
                SettleInteractivePop(recognizer, allowCommit: true);

                break;

            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed:
                SettleInteractivePop(recognizer, allowCommit: false);

                break;
        }
    }

    private void BeginInteractivePop()
    {
        if (scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, MauiContext: { } mauiContext }
            || _currentRoot?.NavigationStack is not { } stack
            || _currentPage is not { } topPage
            || _currentController?.View is not { } topView)
        {
            return;
        }

        if (stack.PushedPages.Count == 0)
        {
            return;
        }

        var belowPage = stack.PushedPages.Count > 1 ? stack.PushedPages[^2].Page : stack.RootPage;

        if (belowPage is null)
        {
            return;
        }

        var container = controller.ContentContainer;
        var belowView = belowPage.ToUIViewController(mauiContext).View!;

        // Peek mount: presentation-only — no controller containment, no page lifecycle. The
        // engine still owns the stack; only the pixels preview the pop.
        belowView.Transform = CGAffineTransform.MakeIdentity();
        belowView.Frame = container.Bounds;
        belowView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        container.InsertSubviewBelow(belowView, topView);

        var session = ScaffoldSharedElementTransitions.BeginInteractivePopSession(
            container,
            mauiContext,
            topPage,
            belowPage,
            topView,
            belowView,
            scaffold.ResolvePageTransition(topPage)
        );

        _interactivePop = new InteractivePopState(topPage, belowPage, belowView, topView, container.Bounds.Width, session);
    }

    private void SettleInteractivePop(UIPanGestureRecognizer recognizer, bool allowCommit)
    {
        if (_interactivePop is not { } state)
        {
            return;
        }

        _interactivePop = null;

        var progress = Math.Clamp((double)recognizer.TranslationInView(recognizer.View).X / state.Width, 0d, 1d);
        var velocity = (double)recognizer.VelocityInView(recognizer.View).X;

        // Guard re-check at release: a guard that appeared mid-gesture cancels the commit.
        var commit = allowCommit
            && (progress > 0.35 || velocity > 500)
            && state.TopPage.BindingContext is not ILeavingGuard;

        SettleAsync().FireAndForget(scaffold.Handler);

        async Task SettleAsync()
        {
            if (!commit)
            {
                await state.Session.CancelAsync();
                UnmountPeek();

                return;
            }

            // Visuals settle forward FIRST (the finger's motion completes uninterrupted), then
            // the pop goes through the engine; the sync it triggers finalizes containment
            // through the handoff below without re-animating.
            await state.Session.FinishAsync();
            _popHandoff = state;

            var popped = scaffold.NavigationService is { } navigationService
                && await navigationService.GoToAsync(Nalu.Navigation.Relative().Pop());

            if (!popped && ReferenceEquals(_popHandoff, state))
            {
                // Engine refused (busy, or a guard surfaced inside the engine): restore the
                // pre-gesture presentation — slide the top page back over the peek, then unmount.
                _popHandoff = null;
                await UIView.AnimateAsync(_transitionDurationSeconds, () => state.TopView.Transform = CGAffineTransform.MakeIdentity());
                UnmountPeek();

                return;
            }

            // Committed: same phantom-touch guard as the cancel path (see UnmountPeek).
            _edgeGesture?.ResetTracking();
        }

        void UnmountPeek()
        {
            // A sync may have adopted this very view as the current page meanwhile (programmatic
            // pop racing the gesture) — never unmount the presented page.
            if (!ReferenceEquals(state.BelowView, _currentController?.View))
            {
                state.BelowView.RemoveFromSuperview();
            }

            // Phantom-touch guard: drop any stale touch bookkeeping so the NEXT edge swipe
            // starts from a clean recognizer (observed on device without this).
            _edgeGesture?.ResetTracking();
        }
    }

    private async Task TransitionToPageAsync(ScaffoldViewController controller, IMauiContext mauiContext, Page targetPage, ScaffoldPresentationHint hint, bool barVisible, bool navBarVisible)
    {
        var parentController = controller.ContentHost;
        var container = controller.ContentContainer;

        // An interactive pop already settled these exact visuals: adopt instead of animating.
        var handoff = _popHandoff;
        _popHandoff = null;
        var interactivelySettled = handoff is not null
            && hint == ScaffoldPresentationHint.Pop
            && ReferenceEquals(targetPage, handoff.BelowPage);

        if (_currentPage is not null)
        {
            _currentPage.PropertyChanged -= OnCurrentPagePropertyChanged;
        }

        var previousPage = _currentPage;
        var previousController = _currentController;
        var newController = targetPage.ToUIViewController(mauiContext);
        _currentPage = targetPage;
        _currentController = newController;
        targetPage.PropertyChanged += OnCurrentPagePropertyChanged;

        // §5.4 per-page inset application: each page is laid out with the insets matching its
        // own chrome visibility from birth — the outgoing page keeps its insets while leaving.
        controller.CurrentPageController = newController;
        controller.CurrentPageWantsBarInset = barVisible;
        controller.CurrentPageWantsNavBarInset = navBarVisible;
        controller.ApplyCurrentPageInsets();

        parentController.AddChildViewController(newController);
        var newView = newController.View!;

        // A remounted page keeps the transform its unmount animation left behind (covered pages
        // are detached, never destroyed) — setting Frame under an active transform corrupts the
        // geometry (the page lands offscreen). Always clear before framing.
        ResetMotion(newView);
        newView.Frame = container.Bounds;
        newView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

        var width = container.Bounds.Width;

        switch (hint)
        {
            case ScaffoldPresentationHint.Pop when interactivelySettled:
            {
                // Peek view already mounted at its final geometry; popped view already offscreen
                // (the gesture session finished the exact pop choreography). Containment only.
                newController.DidMoveToParentViewController(parentController);

                break;
            }

            case ScaffoldPresentationHint.Push:
            {
                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);

                // Shared elements (§8): matching Scaffold.TransitionName pairs fly between the
                // pages while the standard slide plays (the flight math assumes it); pages
                // without pairs play their resolved ScaffoldPageTransition spec (§8.2).
                var handled = previousPage is not null && previousController?.View is { } prevPushView
                    && await ScaffoldSharedElementTransitions.AnimatePushAsync(container, mauiContext, previousPage, targetPage, prevPushView, newView, _transitionDurationSeconds);

                if (!handled)
                {
                    var spec = scaffold.ResolvePageTransition(targetPage);

                    if (spec.IsAnimated)
                    {
                        var previousView = previousController?.View;
                        ApplyMotion(newView, spec.Enter, container.Bounds);

                        await UIView.AnimateAsync(spec.DurationSeconds, () =>
                        {
                            ResetMotion(newView);

                            if (previousView is not null)
                            {
                                ApplyMotion(previousView, spec.Behind, container.Bounds);
                            }
                        });
                    }
                }

                break;
            }

            case ScaffoldPresentationHint.Pop when previousController?.View is { } previousView:
            {
                container.InsertSubviewBelow(newView, previousView);
                newController.DidMoveToParentViewController(parentController);

                var handled = previousPage is not null
                    && await ScaffoldSharedElementTransitions.AnimatePopAsync(container, mauiContext, previousPage, targetPage, previousView, newView, _transitionDurationSeconds);

                if (!handled)
                {
                    // The POPPED page's own spec, reversed: it leaves the way it entered.
                    var spec = previousPage is not null ? scaffold.ResolvePageTransition(previousPage) : ScaffoldPageTransition.Default;

                    if (spec.IsAnimated)
                    {
                        ApplyMotion(newView, spec.Behind, container.Bounds);

                        await UIView.AnimateAsync(spec.DurationSeconds, () =>
                        {
                            ResetMotion(newView);
                            ApplyMotion(previousView, spec.Enter, container.Bounds);
                        });
                    }
                }

                break;
            }

            case ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd:
            {
                // Tab/root switch: both pages slide together in the direction of travel.
                // Logical Start/End mapped LTR for now (RTL mapping arrives with the engine).
                var fromX = hint == ScaffoldPresentationHint.SlideEnd ? width : -width;

                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);
                newView.Transform = CGAffineTransform.MakeTranslation(fromX, 0);

                var previousView = previousController?.View;

                await UIView.AnimateAsync(_transitionDurationSeconds, () =>
                {
                    newView.Transform = CGAffineTransform.MakeIdentity();

                    if (previousView is not null)
                    {
                        previousView.Transform = CGAffineTransform.MakeTranslation(-fromX, 0);
                    }
                });

                break;
            }

            default:
                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);

                break;
        }

        if (previousController is not null)
        {
            previousController.WillMoveToParentViewController(null);

            if (previousController.View is { } previousView)
            {
                previousView.RemoveFromSuperview();

                // Leave the detached view motion-clean for its next mount.
                ResetMotion(previousView);
            }

            previousController.RemoveFromParentViewController();
        }
    }

    /// <summary>Applies a §8.2 motion state: scale about center + fractional translation + opacity.</summary>
    private static void ApplyMotion(UIView view, ScaffoldTransitionMotion motion, CGRect bounds)
    {
        var transform = CGAffineTransform.MakeScale((nfloat)motion.Scale, (nfloat)motion.Scale);
        transform.Tx = (nfloat)(motion.FractionX * bounds.Width);
        transform.Ty = (nfloat)(motion.FractionY * bounds.Height);
        view.Transform = transform;
        view.Alpha = (nfloat)motion.Opacity;
    }

    private static void ResetMotion(UIView view)
    {
        view.Transform = CGAffineTransform.MakeIdentity();
        view.Alpha = 1;
    }

    /// <summary>
    /// Brings the chrome to the desired state. Visibility changes RETARGET any in-flight
    /// slide from its current position (no queue, no teardown): the strip stays mounted while
    /// its area is a tab bar — hidden just means translated offscreen — so rapid toggles
    /// reverse smoothly and re-showing is instant. The bar view's logical attachment still
    /// tracks presented state (the element tree reflects presented chrome).
    /// </summary>
    private Task UpdateTabBarChromeAsync(ScaffoldViewController controller, IMauiContext mauiContext, ScaffoldTabBar? tabBarArea, bool barVisible, bool animated)
    {
        if (tabBarArea is null)
        {
            // Area without a tab bar: tear the strip down entirely (animated slide-out first).
            if (_currentBarView is null)
            {
                return Task.CompletedTask;
            }

            var previousArea = _currentTabBarArea;
            _currentBarView = null;
            _currentTabBarArea = null;

            return UnmountAsync(previousArea);
        }

        if (barVisible)
        {
            var barView = tabBarArea.GetOrCreateBarView();

            if (!ReferenceEquals(barView, _currentBarView))
            {
                var previousArea = _currentTabBarArea;
                _currentBarView = barView;
                _currentTabBarArea = tabBarArea;

                // A freshly appearing bar starts below the edge and slides in with the pop.
                controller.MountTabBar(barView.ToPlatform(mauiContext), startHidden: animated);

                if (previousArea is not null && !ReferenceEquals(previousArea, tabBarArea))
                {
                    previousArea.OnBarViewUnmounted();
                }
            }

            return controller.SetTabBarPresentedAsync(true, animated);
        }

        // Hidden: keep the strip alive offscreen; only the logical attachment reflects it.
        _currentTabBarArea?.OnBarViewUnmounted();

        return controller.SetTabBarPresentedAsync(false, animated);

        async Task UnmountAsync(ScaffoldTabBar? previousArea)
        {
            await controller.SetTabBarPresentedAsync(false, animated);
            controller.UnmountTabBar();
            previousArea?.OnBarViewUnmounted();
        }
    }

    private void OnCurrentPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Page page
            || !ReferenceEquals(page, _currentPage)
            || scaffold.Proxy?.CurrentItem.CurrentSection is not ScaffoldRootProxy rootProxy
            || scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, MauiContext: { } mauiContext })
        {
            return;
        }

        switch (e.PropertyName)
        {
            // Bar visibility is an animated inset change, not a page relayout (§5.4).
            case "TabBarVisibility":
            {
                var tabBarArea = rootProxy.Root.Parent as ScaffoldTabBar;
                var barVisible = tabBarArea is not null && Scaffold.ComputeTabBarVisible(rootProxy.Root, page);
                controller.CurrentPageWantsBarInset = barVisible;
                UpdateTabBarChromeAsync(controller, mauiContext, tabBarArea, barVisible, animated: true).FireAndForget(scaffold.Handler);

                break;
            }

            case "IsNavBarVisible":
            case "NavBarView":
            {
                var navBarView = scaffold.ResolveNavBarView(page);
                var navBarVisible = navBarView is not null && Scaffold.GetIsNavBarVisible(page);
                controller.CurrentPageWantsNavBarInset = navBarVisible;
                UpdateNavBarChromeAsync(controller, mauiContext, navBarView, navBarVisible, animated: true).FireAndForget(scaffold.Handler);

                break;
            }
        }
    }

    /// <summary>
    /// Brings the nav bar chrome to the desired state — same model as the tab bar: the strip
    /// stays mounted while a bar view is resolved (hidden = translated above the screen edge),
    /// visibility changes retarget in-flight slides, and the bar view swaps only when the
    /// resolution changes (page-level custom bars).
    /// </summary>
    private Task UpdateNavBarChromeAsync(ScaffoldViewController controller, IMauiContext mauiContext, View? navBarView, bool navBarVisible, bool animated)
    {
        if (navBarView is null)
        {
            if (_currentNavBarView is { } detachedView)
            {
                _currentNavBarView = null;
                controller.UnmountNavBar();
                DetachNavBarView(detachedView);
            }

            return Task.CompletedTask;
        }

        if (!ReferenceEquals(navBarView, _currentNavBarView))
        {
            if (_currentNavBarView is { } previousView)
            {
                controller.UnmountNavBar();
                DetachNavBarView(previousView);
            }

            _currentNavBarView = navBarView;
            navBarView.BindingContext = scaffold.NavBarContext;

            // A freshly appearing bar starts above the edge and slides in.
            controller.MountNavBar(navBarView.ToPlatform(mauiContext), startHidden: animated);
        }

        // The element tree reflects presented chrome: attached while visible, detached while
        // hidden (the strip and platform view stay alive offscreen either way).
        if (navBarVisible)
        {
            if (navBarView.Parent is null)
            {
                scaffold.AddLogicalChild(navBarView);
            }
        }
        else
        {
            DetachNavBarView(navBarView);
        }

        return controller.SetNavBarPresentedAsync(navBarVisible, animated);
    }

    private void DetachNavBarView(View navBarView)
    {
        if (ReferenceEquals(navBarView.Parent, scaffold))
        {
            scaffold.RemoveLogicalChild(navBarView);
        }
    }

    public Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content)
        => ShowOverlayAsync(
            content,
            side == ScaffoldFlyoutSide.Start ? ScaffoldOverlayPlacement.FlyoutStart : ScaffoldOverlayPlacement.FlyoutEnd,
            scaffold.GetEffectiveFlyoutOptions(side).ComputeScrimColor(),
            behindBottomChrome: false,
            disconnectOnClose: false
        );

    /// <summary>
    /// Maps a logical drawer placement to the physical LEFT edge (false = right): Start is left
    /// in LTR — the single spot the RTL mapping lives in (placement and slide direction).
    /// </summary>
    private bool IsFlyoutOnLeft(ScaffoldOverlayPlacement placement)
        => placement == ScaffoldOverlayPlacement.FlyoutStart != scaffold.IsRightToLeft;

    public async Task OpenTabBarPanelAsync(View content, Color scrimColor, bool disconnectOnClose, Action? cleanup)
    {
        if (HasOverlay)
        {
            cleanup?.Invoke();

            return;
        }

        _overlayCleanup = cleanup;

        await ShowOverlayAsync(content, ScaffoldOverlayPlacement.AboveBottomChrome, scrimColor, behindBottomChrome: true, disconnectOnClose);

        if (!HasOverlay)
        {
            // Presenting failed (no handler/platform view): release the caller's resources.
            _overlayCleanup = null;
            cleanup?.Invoke();
        }
    }

    /// <summary>
    /// §5.6 overlay primitive: scrim + panel. With <paramref name="behindBottomChrome"/>
    /// (reserved for the tab bar overflow panel) the FULLSCREEN scrim and the panel are
    /// inserted BELOW the bottom chrome strip in z-order — the tab bar renders above the scrim,
    /// undimmed and interactive, with no exclusion geometry to maintain.
    /// </summary>
    private async Task ShowOverlayAsync(View content, ScaffoldOverlayPlacement placement, Color scrimColor, bool behindBottomChrome, bool disconnectOnClose)
    {
        if (_overlayPanel is not null
            || scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, PlatformView: { } container, MauiContext: { } mauiContext })
        {
            return;
        }

        var bounds = container.Bounds;
        var chromeLayer = behindBottomChrome ? controller.ChromeBottomLayer : null;
        var excludedBottom = behindBottomChrome ? controller.ChromeBottomFootprint : 0;

        var scrim = new UIView(bounds)
        {
            BackgroundColor = scrimColor.ToPlatform(),
            Alpha = 0,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
        };
        scrim.AddGestureRecognizer(new UITapGestureRecognizer(() => _ = CloseOverlayAsync()));

        if (chromeLayer is not null)
        {
            container.InsertSubviewBelow(scrim, chromeLayer);
        }
        else
        {
            container.AddSubview(scrim);
        }

        var panel = content.ToPlatform(mauiContext);

        switch (placement)
        {
            case ScaffoldOverlayPlacement.FlyoutStart:
            case ScaffoldOverlayPlacement.FlyoutEnd:
            {
                var side = placement == ScaffoldOverlayPlacement.FlyoutStart ? ScaffoldFlyoutSide.Start : ScaffoldFlyoutSide.End;
                var options = scaffold.GetEffectiveFlyoutOptions(side);
                var width = options.ComputeWidth(bounds.Width);
                var onLeft = IsFlyoutOnLeft(placement);
                var offscreenX = onLeft ? -width : bounds.Width;
                var openX = onLeft ? 0 : bounds.Width - width;
                panel.Frame = new CGRect(offscreenX, 0, width, bounds.Height);
                container.AddSubview(panel);

                _overlayScrim = scrim;
                _overlayPanel = panel;
                _overlayContent = disconnectOnClose ? content : null;
                _overlayPlacement = placement;

                await UIView.AnimateAsync(_transitionDurationSeconds, () =>
                {
                    scrim.Alpha = 1;
                    panel.Frame = new CGRect(openX, 0, width, bounds.Height);
                });

                scaffold.OnFlyoutPresented(side);

                break;
            }

            case ScaffoldOverlayPlacement.AboveBottomChrome:
            {
                var margin = content.Margin;
                var maxWidth = bounds.Width - margin.Left - margin.Right;
                var maxHeight = bounds.Height - excludedBottom - _overflowGap - controller.View!.SafeAreaInsets.Top;

                // The panel hugs its content and centers, mirroring the bar pill's own sizing.
                var fitted = panel.SizeThatFits(new CGSize(maxWidth, maxHeight));
                var width = Math.Min((double)fitted.Width, maxWidth);
                var height = Math.Min((double)fitted.Height, maxHeight);

                var y = bounds.Height - excludedBottom - _overflowGap - height;
                panel.Frame = new CGRect((bounds.Width - width) / 2, y, width, height);
                panel.Alpha = 0;
                panel.Transform = CGAffineTransform.MakeTranslation(0, 24);

                if (chromeLayer is not null)
                {
                    container.InsertSubviewBelow(panel, chromeLayer);
                }
                else
                {
                    container.AddSubview(panel);
                }

                _overlayScrim = scrim;
                _overlayPanel = panel;
                _overlayContent = disconnectOnClose ? content : null;
                _overlayPlacement = placement;

                await UIView.AnimateAsync(_transitionDurationSeconds, () =>
                {
                    scrim.Alpha = 1;
                    panel.Alpha = 1;
                    panel.Transform = CGAffineTransform.MakeIdentity();
                });

                break;
            }
        }
    }

    public async Task CloseOverlayAsync()
    {
        if (_overlayPanel is not { } panel || _overlayScrim is not { } scrim)
        {
            return;
        }

        var content = _overlayContent;
        var placement = _overlayPlacement;
        var cleanup = _overlayCleanup;
        _overlayPanel = null;
        _overlayScrim = null;
        _overlayContent = null;
        _overlayCleanup = null;
        cleanup?.Invoke();

        var containerWidth = panel.Superview?.Bounds.Width ?? panel.Frame.Width;

        await UIView.AnimateAsync(_transitionDurationSeconds, () =>
        {
            scrim.Alpha = 0;

            switch (placement)
            {
                case ScaffoldOverlayPlacement.FlyoutStart:
                case ScaffoldOverlayPlacement.FlyoutEnd:
                    panel.Frame = panel.Frame with { X = IsFlyoutOnLeft(placement) ? -panel.Frame.Width : containerWidth };

                    break;

                case ScaffoldOverlayPlacement.AboveBottomChrome:
                    panel.Alpha = 0;
                    panel.Transform = CGAffineTransform.MakeTranslation(0, 24);

                    break;
            }
        });

        panel.RemoveFromSuperview();
        scrim.RemoveFromSuperview();

        if (content is not null)
        {
            content.DisconnectHandlers();
        }

        if (placement is ScaffoldOverlayPlacement.FlyoutStart or ScaffoldOverlayPlacement.FlyoutEnd)
        {
            scaffold.OnFlyoutDismissed(placement == ScaffoldOverlayPlacement.FlyoutStart ? ScaffoldFlyoutSide.Start : ScaffoldFlyoutSide.End);
        }
    }
}
