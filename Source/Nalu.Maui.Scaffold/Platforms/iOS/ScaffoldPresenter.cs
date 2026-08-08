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
internal sealed class ScaffoldPresenter(Scaffold scaffold) : IScaffoldPresenter, IDisposable
{
    private const double _transitionDurationSeconds = 0.25;
    private const double _overflowGap = 8;

    private Page? _currentPage;
    private ScaffoldRoot? _currentRoot;
    private UIViewController? _currentController;
    private ScaffoldTabBar? _currentTabBarArea;
    private View? _currentBarView;
    private ScaffoldNavBarHost? _navBarHost;
    private ScaffoldArea? _observedNavBarArea;
    private bool _scaffoldObserved;

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

    /// <summary>One presented §5.6 overlay entry: scrim + content, stacked in open order.</summary>
    private sealed class OverlayEntry
    {
        public required ScaffoldOverlayRequest Request { get; set; }
        public required View ScrimView { get; init; }
        public required UIView ScrimPlatform { get; init; }
        public required UIView ContentPlatform { get; set; }
        public double FlyoutOffscreenTranslation { get; init; }
        public bool Closing { get; set; }
        public TaskCompletionSource ClosedTcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly List<OverlayEntry> _overlays = [];

    public bool HasOverlay => _overlays.Count > 0;

    public bool IsOverlayPresented(ScaffoldOverlayRequest request) => FindEntry(request) is not null;

    private OverlayEntry? FindEntry(ScaffoldOverlayRequest request)
        => _overlays.Find(entry => ReferenceEquals(entry.Request, request));

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

        // Navigation dismisses every open overlay (flyout, panels, popups).
        await CloseAllOverlaysAsync();

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

        // Overlap mode: the bar still presents, but its footprint is not applied to the page —
        // content lays out from the top edge and the bar draws over it.
        var navBarInsets = navBarVisible && !Scaffold.GetNavBarOverlapsContent(targetPage);
        var animated = hint != ScaffoldPresentationHint.None;

        // The context must carry the target page's state before the bar (or its bindings) mount.
        scaffold.NavBarContext.Update(root, targetPage);

        // Chrome-LEVEL attached changes (scaffold/area NavBarView) must remap live, exactly
        // like the page-level ones the current-page subscription already covers.
        EnsureScaffoldObserver();
        ObserveNavBarArea(scaffold.CurrentArea);

        // Chrome and page animate CONCURRENTLY: an Auto-hiding bar slides away while the pushed
        // page slides in (and back in sync on pop) — no sequential two-phase motion.
        // Nav bar first: its strip must sit BELOW the tab bar strip in z-order.
        var navChromeTask = UpdateNavBarChromeAsync(controller, mauiContext, targetPage, navBarView, navBarVisible, animated);
        var chromeTask = UpdateTabBarChromeAsync(controller, mauiContext, tabBarArea, barVisible, animated);

        var pageTask = ReferenceEquals(targetPage, _currentPage)
            ? Task.CompletedTask
            : TransitionToPageAsync(controller, mauiContext, targetPage, hint, barVisible, navBarInsets);

        await Task.WhenAll(navChromeTask, chromeTask, pageTask);

        // Presentation at rest: the pixels under the status bar are final — read fresh.
        scaffold.SystemBars.OnPresentationSettled();
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
            stack.PushedPages[^1].Page is { } topPage &&
            NavigationHelper.GetLifecycleTarget(topPage) is not ILeavingGuard &&
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
            {
                var progress = (double)recognizer.TranslationInView(recognizer.View).X / state.Width;
                state.Session.SetProgress(progress);
                ScaffoldPageDepth.SetDim(state.BelowView, 1f - (float)Math.Clamp(progress, 0d, 1d));

                break;
            }

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

        // Depth cues: the scrubbed page casts a shadow so its boundary reads against the peek,
        // which starts fully dimmed and brightens as the page departs.
        ScaffoldPageDepth.ApplyShadow(topView);
        ScaffoldPageDepth.SetDim(belowView, 1f);

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
            && NavigationHelper.GetLifecycleTarget(state.TopPage) is not ILeavingGuard;

        SettleAsync().FireAndForget(scaffold.Handler);

        async Task SettleAsync()
        {
            if (!commit)
            {
                // The peek dims back toward covered while the page slides home.
                var cancelDim = ScaffoldPageDepth.AnimateDimAsync(state.BelowView, 1f, _transitionDurationSeconds);
                await state.Session.CancelAsync();
                await cancelDim;
                ScaffoldPageDepth.ClearShadow(state.TopView);
                ScaffoldPageDepth.RemoveDim(state.BelowView);
                UnmountPeek();

                return;
            }

            // Visuals settle forward FIRST (the finger's motion completes uninterrupted), then
            // the pop goes through the engine; the sync it triggers finalizes containment
            // through the handoff below without re-animating.
            var settleDim = ScaffoldPageDepth.AnimateDimAsync(state.BelowView, 0f, _transitionDurationSeconds);
            await state.Session.FinishAsync();
            await settleDim;

            // The peek is about to become the presented page: no residual dim.
            ScaffoldPageDepth.RemoveDim(state.BelowView);
            _popHandoff = state;

            var popped = scaffold.NavigationService is { } navigationService
                && await navigationService.GoToAsync(Nalu.Navigation.Relative().Pop());

            if (!popped && ReferenceEquals(_popHandoff, state))
            {
                // Engine refused (busy, or a guard surfaced inside the engine): restore the
                // pre-gesture presentation — slide the top page back over the peek, then unmount.
                _popHandoff = null;
                await UIView.AnimateAsync(_transitionDurationSeconds, () => state.TopView.Transform = CGAffineTransform.MakeIdentity());
                ScaffoldPageDepth.ClearShadow(state.TopView);
                ScaffoldPageDepth.RemoveDim(state.BelowView);
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

    private async Task TransitionToPageAsync(ScaffoldViewController controller, IMauiContext mauiContext, Page targetPage, ScaffoldPresentationHint hint, bool barVisible, bool wantsNavBarInset)
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

        // BEFORE mounting the incoming page: resign any first responder on the outgoing one so
        // the keyboard dismissal starts (and its insets collapse) ahead of the new page's
        // layout — UIKit would resign it anyway on unmount, but only mid-transition.
        previousController?.View?.EndEditing(true);

        var newController = targetPage.ToUIViewController(mauiContext);
        _currentPage = targetPage;
        _currentController = newController;
        targetPage.PropertyChanged += OnCurrentPagePropertyChanged;

        // MAUI page lifecycle: Disappearing on the covered page, Appearing on the incoming one —
        // raised BEFORE the navigation events, matching the order MAUI's own hosts use.
        ScaffoldPageNavigationEvents.SendAppearanceChange(previousPage, targetPage);

        // MAUI page navigation events: features like HideSoftInputOnTapped are gated on
        // Page.HasNavigatedTo, which only these raise.
        ScaffoldPageNavigationEvents.SendNavigated(previousPage, targetPage, hint.ToNavigationType());

        // §5.4 per-page inset application: each page is laid out with the insets matching its
        // own chrome visibility from birth — the outgoing page keeps its insets while leaving.
        controller.CurrentPageController = newController;
        controller.CurrentPageWantsBarInset = barVisible;
        controller.CurrentPageWantsNavBarInset = wantsNavBarInset;
        controller.ApplyCurrentPageInsets();

        parentController.AddChildViewController(newController);
        var newView = newController.View!;

        // A remounted page keeps the transform its unmount animation left behind (covered pages
        // are detached, never destroyed) — setting Frame under an active transform corrupts the
        // geometry (the page lands offscreen). Always clear before framing; depth cues from a
        // previous departure (shadow, dim) clear with it.
        ResetMotion(newView);
        ScaffoldPageDepth.ClearShadow(newView);
        ScaffoldPageDepth.RemoveDim(newView);
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

                var pushSpec = scaffold.ResolvePageTransition(targetPage);
                var coveredView = previousController?.View;

                // Depth cues span any animated push: the incoming page slides above with a
                // shadow while the covered page dims beneath it.
                var pushAnimates = coveredView is not null
                    && (pushSpec.IsAnimated
                        || (previousPage is not null
                            && ScaffoldTransitions.MatchingNames(ScaffoldTransitions.Collect(previousPage), ScaffoldTransitions.Collect(targetPage)).Count > 0));

                Task? coverDim = null;

                if (pushAnimates)
                {
                    ScaffoldPageDepth.ApplyShadow(newView);
                    ScaffoldPageDepth.SetDim(coveredView!, 0f);
                    coverDim = ScaffoldPageDepth.AnimateDimAsync(coveredView!, 1f, _transitionDurationSeconds);
                }

                // Shared elements (§8): matching Scaffold.TransitionName pairs fly between the
                // pages while the standard slide plays (the flight math assumes it); pages
                // without pairs play their resolved ScaffoldPageTransition spec (§8.2).
                var handled = previousPage is not null && previousController?.View is { } prevPushView
                    && await ScaffoldSharedElementTransitions.AnimatePushAsync(container, mauiContext, previousPage, targetPage, prevPushView, newView, _transitionDurationSeconds);

                if (!handled && pushSpec.IsAnimated)
                {
                    var previousView = previousController?.View;
                    ApplyMotion(newView, pushSpec.Enter, container.Bounds);

                    await UIView.AnimateAsync(pushSpec.DurationSeconds, () =>
                    {
                        ResetMotion(newView);

                        if (previousView is not null)
                        {
                            ApplyMotion(previousView, pushSpec.Behind, container.Bounds);
                        }
                    });
                }

                if (pushAnimates)
                {
                    if (coverDim is not null)
                    {
                        await coverDim;
                    }

                    // The presented page keeps no shadow; the covered page (detached, kept
                    // alive) keeps no dim for its next reveal.
                    ScaffoldPageDepth.ClearShadow(newView);
                    ScaffoldPageDepth.RemoveDim(coveredView!);
                }

                break;
            }

            case ScaffoldPresentationHint.Pop when previousController?.View is { } previousView:
            {
                container.InsertSubviewBelow(newView, previousView);
                newController.DidMoveToParentViewController(parentController);

                // The POPPED page's own spec, reversed: it leaves the way it entered.
                var popSpec = previousPage is not null ? scaffold.ResolvePageTransition(previousPage) : ScaffoldPageTransition.Default;

                // Depth cues span any animated pop: the departing page casts a shadow, the
                // revealed one starts dimmed and brightens as it goes.
                var popAnimates = popSpec.IsAnimated
                    || (previousPage is not null
                        && ScaffoldTransitions.MatchingNames(ScaffoldTransitions.Collect(previousPage), ScaffoldTransitions.Collect(targetPage)).Count > 0);

                Task? revealDim = null;

                if (popAnimates)
                {
                    ScaffoldPageDepth.ApplyShadow(previousView);
                    ScaffoldPageDepth.SetDim(newView, 1f);
                    revealDim = ScaffoldPageDepth.AnimateDimAsync(newView, 0f, _transitionDurationSeconds);
                }

                var handled = previousPage is not null
                    && await ScaffoldSharedElementTransitions.AnimatePopAsync(container, mauiContext, previousPage, targetPage, previousView, newView, _transitionDurationSeconds);

                if (!handled && popSpec.IsAnimated)
                {
                    ApplyMotion(newView, popSpec.Behind, container.Bounds);

                    await UIView.AnimateAsync(popSpec.DurationSeconds, () =>
                    {
                        ResetMotion(newView);
                        ApplyMotion(previousView, popSpec.Enter, container.Bounds);
                    });
                }

                if (popAnimates)
                {
                    if (revealDim is not null)
                    {
                        await revealDim;
                    }

                    ScaffoldPageDepth.RemoveDim(newView);
                    ScaffoldPageDepth.ClearShadow(previousView);
                }

                break;
            }

            case ScaffoldPresentationHint.Fade:
            {
                // Cross-area root switch: no strip to travel along, so the outgoing content
                // fades out ON TOP of the new one (a symmetric double fade would show the
                // window through both of them at the midpoint).
                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);

                if (previousController?.View is { } fadingView)
                {
                    container.BringSubviewToFront(fadingView);

                    await UIView.AnimateAsync(scaffold.ResolveRootSwitchTransition().DurationSeconds, () => fadingView.Alpha = 0);
                }

                break;
            }

            case ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd:
            {
                // Tab/root switch within an area: both pages slide together in the direction of
                // travel. Logical Start/End mapped LTR for now (RTL mapping arrives with the engine).
                var fromX = hint == ScaffoldPresentationHint.SlideEnd ? width : -width;

                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);
                newView.Transform = CGAffineTransform.MakeTranslation(fromX, 0);

                var previousView = previousController?.View;

                await UIView.AnimateAsync(scaffold.ResolveRootSwitchTransition().DurationSeconds, () =>
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
                var previousBarView = _currentBarView;
                _currentBarView = barView;
                _currentTabBarArea = tabBarArea;

                // A freshly appearing bar starts below the edge and slides in with the pop.
                controller.MountTabBar(barView.ToPlatform(mauiContext), startHidden: animated);

                if (previousArea is not null && !ReferenceEquals(previousArea, tabBarArea))
                {
                    // Area switch: the outgoing area's bar stays alive for the return.
                    previousArea.OnBarViewUnmounted();
                }
                else if (previousBarView is not null && ReferenceEquals(previousArea, tabBarArea))
                {
                    // Same-area live swap (runtime replacement / XAML hot reload): the old bar
                    // is gone for good.
                    tabBarArea.OnBarViewReplaced(previousBarView);
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
            case "NavBarOverlapsContent":
                RefreshNavBarChrome();

                break;
        }
    }

    /// <summary>Lazily observes the scaffold itself: chrome-level attached changes remap live.</summary>
    private void EnsureScaffoldObserver()
    {
        if (!_scaffoldObserved)
        {
            _scaffoldObserved = true;
            scaffold.PropertyChanged += OnChromeSourcePropertyChanged;
        }
    }

    /// <summary>Follows the current area (chrome-level NavBarView changes on it remap live).</summary>
    private void ObserveNavBarArea(ScaffoldArea? area)
    {
        if (ReferenceEquals(_observedNavBarArea, area))
        {
            return;
        }

        if (_observedNavBarArea is not null)
        {
            _observedNavBarArea.PropertyChanged -= OnChromeSourcePropertyChanged;
        }

        _observedNavBarArea = area;

        if (area is not null)
        {
            area.PropertyChanged += OnChromeSourcePropertyChanged;
        }
    }

    private void OnChromeSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Appearance changes are observed by the host itself; only the mounted-bar resolution
        // needs the presenter.
        if (e.PropertyName == "NavBarView")
        {
            RefreshNavBarChrome();
        }
        else if (e.PropertyName == "TabBarView")
        {
            RefreshTabBarChrome();
        }
    }

    /// <summary>Re-mounts the tab bar chrome after a live <c>TabBarView</c> swap (runtime replacement or XAML hot reload).</summary>
    private void RefreshTabBarChrome()
    {
        if (_currentPage is not { } page
            || _currentRoot is not { } root
            || scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, MauiContext: { } mauiContext })
        {
            return;
        }

        var tabBarArea = root.Parent as ScaffoldTabBar;
        var barVisible = tabBarArea is not null && Scaffold.ComputeTabBarVisible(root, page);
        UpdateTabBarChromeAsync(controller, mauiContext, tabBarArea, barVisible, animated: false).FireAndForget(scaffold.Handler);
    }

    /// <summary>Re-resolves and re-presents the nav bar chrome for the current page.</summary>
    private void RefreshNavBarChrome()
    {
        if (_currentPage is not { } page
            || scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, MauiContext: { } mauiContext })
        {
            return;
        }

        var navBarView = scaffold.ResolveNavBarView(page);
        var navBarVisible = navBarView is not null && Scaffold.GetIsNavBarVisible(page);
        controller.CurrentPageWantsNavBarInset = navBarVisible && !Scaffold.GetNavBarOverlapsContent(page);
        UpdateNavBarChromeAsync(controller, mauiContext, page, navBarView, navBarVisible, animated: true).FireAndForget(scaffold.Handler);
    }

    /// <summary>
    /// Brings the nav bar chrome to the desired state — same model as the tab bar: the strip
    /// stays mounted while a bar view is resolved (hidden = translated above the screen edge)
    /// and visibility changes retarget in-flight slides. The strip hosts the library-owned
    /// <see cref="ScaffoldNavBarHost"/> (mounted once): bar-view resolution changes swap the
    /// bar VIRTUALLY inside it (instant, no strip re-mount), and the effective
    /// <see cref="ScaffoldNavBarAppearance"/> lands on the host — never on the bar view.
    /// </summary>
    private Task UpdateNavBarChromeAsync(ScaffoldViewController controller, IMauiContext mauiContext, Page targetPage, View? navBarView, bool navBarVisible, bool animated)
    {
        EnsureSystemBarApplier(controller);
        scaffold.SystemBars.NavBarVisible = navBarView is not null && navBarVisible;

        if (navBarView is null)
        {
            if (_navBarHost is { } clearedHost)
            {
                if (clearedHost.Bar is not null)
                {
                    controller.UnmountNavBar();
                    DetachNavBarHost(clearedHost);
                    clearedHost.SetBar(null);
                }

                // The host keeps tracking the CURRENT page even bar-less: the previous page's
                // scroll observation (KVO / listeners) must not outlive its page.
                clearedHost.UpdateSources(targetPage);
            }

            return Task.CompletedTask;
        }

        var host = _navBarHost ??= new ScaffoldNavBarHost(scaffold);
        var freshMount = host.Bar is null;
        host.SetBar(navBarView);
        host.UpdateSources(targetPage);

        if (freshMount)
        {
            // A freshly appearing strip starts above the edge and slides in.
            controller.MountNavBar(host.ToPlatform(mauiContext), startHidden: animated);
        }

        // The element tree reflects presented chrome: attached while visible, detached while
        // hidden (the strip and platform view stay alive offscreen either way).
        if (navBarVisible)
        {
            if (host.Parent is null)
            {
                scaffold.AddLogicalChild(host);
            }
        }
        else
        {
            DetachNavBarHost(host);
        }

        return controller.SetNavBarPresentedAsync(navBarVisible, animated);
    }

    private void DetachNavBarHost(ScaffoldNavBarHost host)
    {
        if (ReferenceEquals(host.Parent, scaffold))
        {
            scaffold.RemoveLogicalChild(host);
        }
    }

    /// <summary>
    /// Routes the resolved system-bar icon style to the controller (UIKit fades the change) and
    /// installs the pixel sampler — a tiny scaled snapshot of the window strip under the status
    /// bar (the status bar itself lives in a system layer, never part of the render).
    /// </summary>
    private void EnsureSystemBarApplier(ScaffoldViewController controller)
    {
        if (!_systemBarApplierAttached)
        {
            _systemBarApplierAttached = true;
            scaffold.SystemBars.SetApplier(controller.SetLightSystemBars);
            scaffold.SystemBars.SetSampler(() => Task.FromResult(SampleStatusBarStrip(controller.View)));
        }
    }

    private bool _systemBarApplierAttached;

    /// <summary>
    /// Average luminance [0, 1] of the app content under the status bar: the window's top strip
    /// rendered scaled into a 32×4 RGBA bitmap context (last committed frame — cheap, GPU-backed).
    /// </summary>
    private static double? SampleStatusBarStrip(UIView? root)
    {
        if (root?.Window is not { } window)
        {
            return null;
        }

        var stripHeight = (double)window.SafeAreaInsets.Top;
        var width = (double)window.Bounds.Width;

        if (stripHeight < 1 || width < 1)
        {
            return null;
        }

        const int sampleWidth = 32;
        const int sampleHeight = 4;

        var format = new UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
        var renderer = new UIGraphicsImageRenderer(new CGSize(sampleWidth, sampleHeight), format);

        var image = renderer.CreateImage(context =>
        {
            context.CGContext.ScaleCTM((nfloat)(sampleWidth / width), (nfloat)(sampleHeight / stripHeight));
            window.DrawViewHierarchy(new CGRect(0, 0, width, window.Bounds.Height), afterScreenUpdates: false);
        });

        if (image.CGImage is not { } cgImage)
        {
            return null;
        }

        // Normalize into a KNOWN layout (RGBA, 8 bpc) — renderer output byte order is device-defined.
        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        using var bitmapContext = new CGBitmapContext(null, sampleWidth, sampleHeight, 8, sampleWidth * 4, colorSpace, CGImageAlphaInfo.PremultipliedLast);

        if (bitmapContext.Data == IntPtr.Zero)
        {
            return null;
        }

        bitmapContext.DrawImage(new CGRect(0, 0, sampleWidth, sampleHeight), cgImage);

        var buffer = new byte[sampleWidth * sampleHeight * 4];
        System.Runtime.InteropServices.Marshal.Copy(bitmapContext.Data, buffer, 0, buffer.Length);

        double total = 0;

        for (var i = 0; i < sampleWidth * sampleHeight; i++)
        {
            var r = buffer[i * 4];
            var g = buffer[(i * 4) + 1];
            var b = buffer[(i * 4) + 2];
            total += ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)) / 255.0;
        }

        return total / (sampleWidth * sampleHeight);
    }

    /// <summary>Releases the scaffold-lifetime subscriptions (handler disconnection).</summary>
    public void Dispose()
    {
        if (_scaffoldObserved)
        {
            _scaffoldObserved = false;
            scaffold.PropertyChanged -= OnChromeSourcePropertyChanged;
        }

        if (_systemBarApplierAttached)
        {
            _systemBarApplierAttached = false;
            scaffold.SystemBars.SetSampler(null);
            scaffold.SystemBars.SetApplier(null);
        }

        ObserveNavBarArea(null);
        _navBarHost?.Dispose();
        _navBarHost = null;
    }

    /// <summary>
    /// Maps a logical drawer side to the physical LEFT edge (false = right): Start is left
    /// in LTR — the single spot the RTL mapping lives in (placement and slide direction).
    /// </summary>
    private bool IsFlyoutOnLeft(ScaffoldFlyoutSide side)
        => side == ScaffoldFlyoutSide.Start != scaffold.IsRightToLeft;

    /// <summary>
    /// The scrim tap rides a MAUI recognizer (uniform hit-testing on both platforms, visible to
    /// automation); it always consumes the touch — closing only when the entry allows it.
    /// </summary>
    private void AttachScrimTap(View scrimView, ScaffoldOverlayRequest request)
    {
        var tap = new TapGestureRecognizer();

        tap.Tapped += (_, _) =>
        {
            if (request.CloseOnScrimTap)
            {
                _ = CloseOverlayAsync(request);
            }
        };

        scrimView.GestureRecognizers.Add(tap);
    }

    public async Task<bool> ShowOverlayAsync(ScaffoldOverlayRequest request)
    {
        if (scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, PlatformView: { } container, MauiContext: { } mauiContext })
        {
            request.Cleanup?.Invoke();

            return false;
        }

        var bounds = container.Bounds;

        // The tab bar panel slot sits BELOW the bottom chrome strip in z-order — the bar
        // renders above the scrim, undimmed and interactive. Everything else stacks on top.
        var chromeLayer = request.Kind == ScaffoldOverlayKind.TabBarPanel ? controller.ChromeBottomLayer : null;

        var scrimView = request.CreateScrimView();

        // The element tree reflects presented chrome: the scrim participates while mounted
        // (tooling and UI tests can see and tap it).
        scaffold.AddLogicalChild(scrimView);
        AttachScrimTap(scrimView, request);
        var scrim = scrimView.ToPlatform(mauiContext);
        scrim.Frame = bounds;
        scrim.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

        if (chromeLayer is not null)
        {
            container.InsertSubviewBelow(scrim, chromeLayer);
        }
        else
        {
            container.AddSubview(scrim);
        }

        double flyoutOffscreen = 0;
        UIView panel;

        switch (request.Kind)
        {
            case ScaffoldOverlayKind.Flyout:
            {
                var options = scaffold.GetEffectiveFlyoutOptions(request.FlyoutSide);
                var width = options.ComputeWidth(bounds.Width);
                var onLeft = IsFlyoutOnLeft(request.FlyoutSide);

                // Arranged VIRTUALLY at the OPEN position (the MAUI frame must be valid or the
                // iOS transform mapper skips translations); the entrance offset rides the MAUI
                // translation, applied after the arrange.
                panel = request.Content.ToPlatform(mauiContext);
                var flyoutView = (IView)request.Content;
                flyoutView.Measure(width, bounds.Height);
                flyoutView.Arrange(new Rect(onLeft ? 0 : bounds.Width - width, 0, width, bounds.Height));
                flyoutOffscreen = onLeft ? -width : width;
                container.AddSubview(panel);

                // The flyout covers the status-bar region: its surface drives the icon style
                // while open (UIKit fades the flip alongside the slide).
                scaffold.SystemBars.OverlaySurface = ScaffoldSystemBars.SurfaceColorOf(request.Content);

                break;
            }

            case ScaffoldOverlayKind.Popup:
            {
                var insets = controller.View!.SafeAreaInsets;

                var presentation = request.PopupPresentation!;
                var margin = presentation.Margin;

                var area = new Rect(
                    bounds.X + insets.Left + margin.Left,
                    bounds.Y + insets.Top + margin.Top,
                    Math.Max(0, bounds.Width - insets.Left - insets.Right - margin.HorizontalThickness),
                    Math.Max(0, bounds.Height - insets.Top - insets.Bottom - margin.VerticalThickness)
                );

                panel = request.Content.ToPlatform(mauiContext);

                var popupView = (IView)request.Content;
                var fitted = popupView.Measure(area.Width, area.Height);
                var contentSize = new Size(Math.Min(fitted.Width, area.Width), Math.Min(fitted.Height, area.Height));

                Rect? anchorBounds = null;

                if (presentation.Anchor is { Handler.PlatformView: UIView anchorView })
                {
                    var frame = anchorView.ConvertRectToView(anchorView.Bounds, container);
                    anchorBounds = new Rect(frame.X, frame.Y, frame.Width, frame.Height);
                }

                var rect = ScaffoldPopupPlacementResolver.Resolve(presentation, area, contentSize, anchorBounds, scaffold.IsRightToLeft);
                popupView.Arrange(rect);
                container.AddSubview(panel);

                break;
            }

            case ScaffoldOverlayKind.BottomSheet:
            {
                var sheet = (ScaffoldBottomSheetView)request.Content;
                var insets = controller.View!.SafeAreaInsets;
                var availableHeight = bounds.Height - insets.Top;

                // Padding first (it affects the natural height), then measure, then geometry.
                sheet.PrepareForMeasure(insets.Bottom);
                panel = request.Content.ToPlatform(mauiContext);
                var sheetView = (IView)request.Content;
                var sheetWidth = Math.Min((double)bounds.Width, sheet.MaxWidth);
                var natural = sheetView.Measure(sheetWidth, (double)availableHeight).Height;
                var sheetHeight = sheet.InitializeGeometry((double)availableHeight, Math.Min(natural, (double)availableHeight));

                // Bottom-anchored, centered at the (possibly capped) width; the sheet's own
                // TranslationY does the rest. Virtual arrange: a valid MAUI frame is required
                // for translations to apply.
                sheetView.Arrange(new Rect((bounds.Width - sheetWidth) / 2, bounds.Height - sheetHeight, sheetWidth, sheetHeight));
                container.AddSubview(panel);

                // Native cooperative drag: the MAUI pan is skipped on iOS (it would beat the
                // inner scroll view's pan) — see ScaffoldBottomSheetGesture.
                ScaffoldBottomSheetGesture.Attach(sheet, panel);

                break;
            }

            default:
            {
                panel = MountTabBarPanelContent(request.Content, container, controller, mauiContext, chromeLayer);

                break;
            }
        }

        var entry = new OverlayEntry
        {
            Request = request,
            ScrimView = scrimView,
            ScrimPlatform = scrim,
            ContentPlatform = panel,
            FlyoutOffscreenTranslation = flyoutOffscreen
        };

        _overlays.Add(entry);

        ScaffoldOverlayAnimations.PrepareEnter(request, flyoutOffscreen);
        await ScaffoldOverlayAnimations.EnterAsync(request, scrimView);

        return true;
    }

    /// <summary>
    /// Fits and mounts a tab bar panel at its resting position: hugging its content, centered,
    /// above the bottom chrome footprint (inserted below the strip when present).
    /// </summary>
    private UIView MountTabBarPanelContent(View content, UIView container, ScaffoldViewController controller, IMauiContext mauiContext, UIView? chromeLayer)
    {
        var bounds = container.Bounds;
        var excludedBottom = chromeLayer is not null ? controller.ChromeBottomFootprint : 0;
        var margin = content.Margin;
        var maxWidth = bounds.Width - margin.Left - margin.Right;
        var maxHeight = bounds.Height - excludedBottom - _overflowGap - controller.View!.SafeAreaInsets.Top;

        var panel = content.ToPlatform(mauiContext);

        // The panel hugs its content and centers, mirroring the bar pill's own sizing. Virtual
        // measure/arrange: a valid MAUI frame is required for the transform mappers to apply.
        var panelView = (IView)content;
        var fitted = panelView.Measure(maxWidth, (double)maxHeight);
        var width = Math.Min(fitted.Width, maxWidth);
        var height = Math.Min(fitted.Height, (double)maxHeight);

        var y = bounds.Height - excludedBottom - _overflowGap - height;
        panelView.Arrange(new Rect((bounds.Width - width) / 2, y, width, height));

        if (chromeLayer is not null)
        {
            container.InsertSubviewBelow(panel, chromeLayer);
        }
        else
        {
            container.AddSubview(panel);
        }

        return panel;
    }

    public async Task ReplaceTabBarPanelAsync(ScaffoldOverlayRequest replacement)
    {
        var entry = _overlays.Find(static candidate => candidate.Request.Kind == ScaffoldOverlayKind.TabBarPanel && !candidate.Closing);

        if (entry is null)
        {
            await ShowOverlayAsync(replacement);

            return;
        }

        if (scaffold.Handler is not IPlatformViewHandler { ViewController: ScaffoldViewController controller, PlatformView: { } container, MauiContext: { } mauiContext })
        {
            replacement.Cleanup?.Invoke();

            return;
        }

        var previous = entry.Request;
        entry.Request = replacement;

        // Scrim: brush update only, no re-animation.
        entry.ScrimView.Background = replacement.Scrim;

        await previous.Content.FadeTo(0, 100);
        entry.ContentPlatform.RemoveFromSuperview();
        ScaffoldOverlayAnimations.ResetContent(previous.Content);
        previous.Cleanup?.Invoke();

        if (previous.DisconnectContentOnClose)
        {
            previous.Content.DisconnectHandlers();
        }

        var chromeLayer = controller.ChromeBottomLayer;
        var panel = MountTabBarPanelContent(replacement.Content, container, controller, mauiContext, chromeLayer);
        entry.ContentPlatform = panel;

        replacement.Content.Opacity = 0;
        await replacement.Content.FadeTo(1, 100);
    }

    public Task CloseOverlayAsync(ScaffoldOverlayRequest request)
        => FindEntry(request) is { } entry ? CloseEntryAsync(entry) : Task.CompletedTask;

    public Task CloseTopOverlayAsync()
        => _overlays.Count > 0 && _overlays[^1] is { Request.CloseOnBack: true } top
            ? CloseEntryAsync(top)
            : Task.CompletedTask;

    public Task CloseAllOverlaysAsync()
        => _overlays.Count == 0
            ? Task.CompletedTask
            : Task.WhenAll(_overlays.ToArray().Select(CloseEntryAsync));

    private async Task CloseEntryAsync(OverlayEntry entry)
    {
        if (entry.Closing)
        {
            // Concurrent close: ride the in-flight one (same UI context; RunContinuationsAsynchronously).
#pragma warning disable VSTHRD003
            await entry.ClosedTcs.Task;
#pragma warning restore VSTHRD003

            return;
        }

        entry.Closing = true;
        var request = entry.Request;

        // Owner cleanup runs BEFORE the exit animation (matching the original primitive):
        // state clears immediately, so a rapid re-open is never blocked by the animation tail.
        request.Cleanup?.Invoke();

        if (request.Kind == ScaffoldOverlayKind.Flyout)
        {
            // The icons return to the underlying resolution as the flyout starts sliding away.
            scaffold.SystemBars.OverlaySurface = null;
        }

        await ScaffoldOverlayAnimations.ExitAsync(request, entry.ScrimView, entry.FlyoutOffscreenTranslation);

        entry.ContentPlatform.RemoveFromSuperview();
        entry.ScrimPlatform.RemoveFromSuperview();
        _overlays.Remove(entry);

        ScaffoldOverlayAnimations.ResetContent(request.Content);
        entry.ScrimView.DisconnectHandlers();
        scaffold.RemoveLogicalChild(entry.ScrimView);

        if (request.DisconnectContentOnClose)
        {
            request.Content.DisconnectHandlers();
        }

        entry.ClosedTcs.TrySetResult();
    }
}
