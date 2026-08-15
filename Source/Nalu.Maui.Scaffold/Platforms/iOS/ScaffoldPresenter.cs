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

        // Presented overlays keep the geometry of the window they were shown in: re-lay them out
        // when it changes shape.
        controller.WindowGeometryChanged ??= () => RelayoutOverlays(controller, controller.ContentContainer);

        // ...and when the soft keyboard changes its overlap: sheets and popups are re-placed against
        // the area ABOVE it. Animated (unlike a shape change): the pass raising this typically runs
        // inside UIKit's keyboard animation, and a sheet stepping out of the keyboard's way should
        // travel with it. Only the sheet and popup slots depend on the keyboard, so this is not the
        // full RelayoutOverlays (which also dismisses the tab bar panel).
        controller.KeyboardOverlapChanged ??= () => RelayoutKeyboardAwareOverlays(controller, controller.ContentContainer);
        controller.OverlayOwnsKeyboard ??= () => KeyboardOwner is not null;

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

        // Depth cue: the peek starts fully dimmed and brightens as the scrubbed page departs.
        ScaffoldPageDepth.SetDim(belowView, 1f);

        var session = ScaffoldSharedElementTransitions.BeginInteractivePopSession(
            container,
            mauiContext,
            topPage,
            belowPage,
            topView,
            belowView
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
        controller.CurrentPageKeyboardMode ??= () => scaffold.ResolvePageKeyboardMode(_currentPage);
        controller.ApplyCurrentPageInsets();

        parentController.AddChildViewController(newController);
        var newView = newController.View!;

        // A remounted page keeps the transform its unmount animation left behind (covered pages
        // are detached, never destroyed) — every choreography below starts from identity, so a
        // survivor would offset the page for its whole entrance (it lands offscreen). Always
        // clear before staging; the depth dim from a previous departure clears with it.
        ResetMotion(newView);
        ScaffoldPageDepth.RemoveDim(newView);

        var width = container.Bounds.Width;

        // THE mount step, shared by every choreography below: stage the page and complete
        // containment. One helper on purpose — each branch used to repeat these calls, and a
        // branch that got them wrong shipped a visual bug of its own.
        void Mount()
        {
            container.AddSubview(newView);
            UIView.PerformWithoutAnimation(CompleteMount);
        }
        
        // The caller has already matched the view it wants the page staged under — take it, rather
        // than reaching back through the controller for a nullable that was checked elsewhere.
        void MountBelow(UIView below)
        {
            container.InsertSubviewBelow(newView, below);
            UIView.PerformWithoutAnimation(CompleteMount);
        }

        void CompleteMount()
        {
            // Size the page to the container EXPLICITLY: nothing else does it (the controller
            // frames its content host, never the page), and the autoresizing mask below only
            // reacts to LATER container resizes — it cannot correct a wrong starting size. Left
            // implicit, a fresh page would arrive at whatever size UIKit loaded its controller's
            // view at, which happens to match only while the container fills the window.
            //
            // Bounds+Center rather than Frame: under a non-identity transform the frame is
            // undefined, and every page here is moved by transform — the slides, the
            // shared-element flights, the interactive peek.
            var bounds = container.Bounds;
            newView.Bounds = new CGRect(CGPoint.Empty, bounds.Size);
            newView.Center = new CGPoint(bounds.GetMidX(), bounds.GetMidY());

            newView.TranslatesAutoresizingMaskIntoConstraints = true;
            newView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
            newController.DidMoveToParentViewController(parentController);

            // Arranges the page's content from the CONTAINER as layout root, outside any
            // animation. The chrome show/hide accompanying a navigation runs its own layout pass
            // inside an animation block, and UIKit animates every frame change it produces: a
            // page first arranged in there has each child interpolated from a never-arranged zero
            // frame, so it appears to SCALE IN instead of sliding in already laid out.
            container.LayoutIfNeeded();
        }

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
                Mount();
                await PlayPushAsync(container, mauiContext, previousPage, targetPage, previousController, newView);

                break;
            }

            case ScaffoldPresentationHint.Pop when previousController?.View is { } previousView:
            {
                MountBelow(previousView);
                await PlayPopAsync(container, mauiContext, previousPage, targetPage, previousView, newView);

                break;
            }

            case ScaffoldPresentationHint.Fade:
            {
                // Cross-area root switch: no strip to travel along, so the outgoing content
                // fades out ON TOP of the new one (a symmetric double fade would show the
                // window through both of them at the midpoint).
                Mount();
                await PlayCrossAreaFadeAsync(container, previousController);

                break;
            }

            case ScaffoldPresentationHint.SlideStart or ScaffoldPresentationHint.SlideEnd:
            {
                // Tab/root switch within an area: both pages slide together in the direction of
                // travel. Logical Start/End mapped LTR for now (RTL mapping arrives with the engine).
                var fromX = hint == ScaffoldPresentationHint.SlideEnd ? width : -width;

                Mount();
                await PlaySwitchSlideAsync(previousController, newView, fromX);

                break;
            }

            default:
                Mount();

                break;
        }

        DetachPreviousPage(previousController);
    }

    /// <summary>Unmounts the page left behind, motion-clean for its next appearance.</summary>
    private static void DetachPreviousPage(UIViewController? previousController)
    {
        if (previousController is null)
        {
            return;
        }

        previousController.WillMoveToParentViewController(null);

        if (previousController.View is { } previousView)
        {
            previousView.RemoveFromSuperview();

            ResetMotion(previousView);
        }

        previousController.RemoveFromParentViewController();
    }

    /// <summary>
    /// The stock push: the covered page dims beneath the incoming one, shared-element pairs fly
    /// if the two pages declare matching names, otherwise both play the resolved §8.2 spec.
    /// </summary>
    private async Task PlayPushAsync(
        UIView container,
        IMauiContext mauiContext,
        Page? previousPage,
        Page targetPage,
        UIViewController? previousController,
        UIView newView)
    {
        var pushSpec = scaffold.ResolvePageTransition(targetPage);
        var coveredView = previousController?.View;

        // Depth cue spans any animated push: the covered page dims beneath the
        // incoming one.
        var pushAnimates = coveredView is not null
            && (pushSpec.IsAnimated
                || (previousPage is not null
                    && ScaffoldTransitions.MatchingNames(ScaffoldTransitions.Collect(previousPage), ScaffoldTransitions.Collect(targetPage)).Count > 0));

        Task? coverDim = null;

        if (pushAnimates)
        {
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

            // The covered page (detached, kept alive) keeps no dim for its next
            // reveal.
            ScaffoldPageDepth.RemoveDim(coveredView!);
        }
    }

    /// <summary>
    /// The stock pop: the revealed page starts dimmed and brightens while the departing page
    /// leaves the way it entered — its OWN spec, reversed.
    /// </summary>
    private async Task PlayPopAsync(
        UIView container,
        IMauiContext mauiContext,
        Page? previousPage,
        Page targetPage,
        UIView previousView,
        UIView newView)
    {
        // The POPPED page's own spec, reversed: it leaves the way it entered.
        var popSpec = previousPage is not null ? scaffold.ResolvePageTransition(previousPage) : ScaffoldPageTransition.Default;

        // Depth cue spans any animated pop: the revealed page starts dimmed and
        // brightens as the departing one goes.
        var popAnimates = popSpec.IsAnimated
            || (previousPage is not null
                && ScaffoldTransitions.MatchingNames(ScaffoldTransitions.Collect(previousPage), ScaffoldTransitions.Collect(targetPage)).Count > 0);

        Task? revealDim = null;

        if (popAnimates)
        {
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
        }
    }

    /// <summary>
    /// Cross-area root switch: no strip to travel along, so the outgoing content fades out ON TOP
    /// of the new one (a symmetric double fade would show the window through both at the midpoint).
    /// </summary>
    private async Task PlayCrossAreaFadeAsync(UIView container, UIViewController? previousController)
    {
        if (previousController?.View is { } fadingView)
        {
            container.BringSubviewToFront(fadingView);

            await UIView.AnimateAsync(scaffold.ResolveRootSwitchTransition().DurationSeconds, () => fadingView.Alpha = 0);
        }
    }

    /// <summary>
    /// Tab or root switch within an area: both pages travel together in the direction of the
    /// switch. Logical Start/End mapped LTR for now (RTL mapping arrives with the engine).
    /// </summary>
    private async Task PlaySwitchSlideAsync(UIViewController? previousController, UIView newView, nfloat fromX)
    {
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

        // Bar-subtree measure changes reach the strip through the host's Controls-layer bubble —
        // the platform invalidation walk dies inside the host chain (see BarMeasureInvalidated).
        host.BarMeasureInvalidated = controller.InvalidateNavBarMeasure;

        var freshMount = host.Bar is null;
        host.SetBar(navBarView);
        host.UpdateSources(targetPage);

        if (freshMount)
        {
            // A freshly appearing strip starts above the edge and slides in.
            controller.MountNavBar(host.ToPlatform(mauiContext), startHidden: animated);
        }
        else
        {
            // The strip keeps the same platform host across swaps, so its measure still describes
            // the PREVIOUS bar: a shorter custom bar would be centered inside the taller strip it
            // replaced. MAUI's own measure invalidation stops at the native strip, so ask here.
            controller.InvalidateNavBarMeasure();
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
                // Arranged VIRTUALLY at the OPEN position (the MAUI frame must be valid or the
                // iOS transform mapper skips translations); the entrance offset rides the MAUI
                // translation, applied after the arrange.
                panel = request.Content.ToPlatform(mauiContext);
                flyoutOffscreen = LayoutFlyout(request, container);
                container.AddSubview(panel);

                // The flyout covers the status-bar region: its surface drives the icon style
                // while open (UIKit fades the flip alongside the slide).
                scaffold.SystemBars.OverlaySurface = ScaffoldSystemBars.SurfaceColorOf(request.Content);

                break;
            }

            case ScaffoldOverlayKind.Popup:
            {
                panel = request.Content.ToPlatform(mauiContext);
                LayoutPopup(request, controller, container, controller.KeyboardOverlap);
                container.AddSubview(panel);

                break;
            }

            case ScaffoldOverlayKind.BottomSheet:
            {
                var sheet = (ScaffoldBottomSheetView)request.Content;
                panel = request.Content.ToPlatform(mauiContext);
                LayoutBottomSheet(sheet, request.KeyboardMode, controller, container, initial: true, controller.KeyboardOverlap);
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

        // A new sheet/popup takes the keyboard over from the page (or the entry below it).
        if (request.Kind is ScaffoldOverlayKind.Popup or ScaffoldOverlayKind.BottomSheet)
        {
            OnKeyboardOwnerChanged(controller);
        }

        ScaffoldOverlayAnimations.PrepareEnter(request, flyoutOffscreen);
        await ScaffoldOverlayAnimations.EnterAsync(request, scrimView);

        return true;
    }

    /// <summary>
    /// Arranges a flyout against the CURRENT window — full height, its configured width, pinned to
    /// its edge — and returns the offscreen translation that side implies.
    /// </summary>
    /// <remarks>
    /// The width is a function of the window width (a fraction, typically), and the END side is
    /// positioned from the right edge: a window that changes shape moves both. The arrange leaves
    /// TranslationX alone, so an open flyout stays open and a closing one keeps animating.
    /// </remarks>
    private double LayoutFlyout(ScaffoldOverlayRequest request, UIView container)
    {
        var bounds = container.Bounds;
        var options = scaffold.GetEffectiveFlyoutOptions(request.FlyoutSide);
        var width = options.ComputeWidth(bounds.Width);
        var onLeft = IsFlyoutOnLeft(request.FlyoutSide);

        var flyoutView = (IView)request.Content;
        flyoutView.Measure(width, bounds.Height);
        flyoutView.Arrange(new Rect(onLeft ? 0 : bounds.Width - width, 0, width, bounds.Height));

        return onLeft ? -width : width;
    }

    /// <summary>
    /// Resolves a popup's placement against the CURRENT window: the available area is the window
    /// minus its safe-area insets, minus the soft keyboard's overlap (a keyboard is a bottom inset
    /// for placement purposes: an anchored popup that no longer fits below flips above, a centered
    /// one centers in what is left), minus the popup's margin; an anchored popup follows wherever
    /// its anchor now sits.
    /// </summary>
    private void LayoutPopup(ScaffoldOverlayRequest request, ScaffoldViewController controller, UIView container, double keyboardOverlap)
    {
        var bounds = container.Bounds;
        var insets = controller.View!.SafeAreaInsets;

        // Resize: the keyboard shrinks the placement area. Pan / None: the popup is placed as if
        // there were no keyboard (Pan then slides it — below).
        var bottomInset = request.KeyboardMode == ScaffoldKeyboardMode.Resize
            ? ScaffoldOverlayGeometry.BottomInset(insets.Bottom, keyboardOverlap)
            : (double)insets.Bottom;

        var presentation = request.PopupPresentation!;
        var margin = presentation.Margin;

        var area = new Rect(
            bounds.X + insets.Left + margin.Left,
            bounds.Y + insets.Top + margin.Top,
            Math.Max(0, bounds.Width - insets.Left - insets.Right - margin.HorizontalThickness),
            Math.Max(0, bounds.Height - insets.Top - bottomInset - margin.VerticalThickness)
        );

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

        if (request.KeyboardMode == ScaffoldKeyboardMode.Pan && keyboardOverlap > 0)
        {
            var focusedBottom = request.Content.Handler?.PlatformView is UIView panel ? ScaffoldFocusedInput.BottomIn(panel) : null;

            rect.Y -= ScaffoldOverlayGeometry.Pan(
                bounds.Height - keyboardOverlap,
                rect.Top,
                rect.Bottom,
                focusedBottom is { } inPanel ? rect.Top + inPanel : null,
                insets.Top + margin.Top
            );
        }

        popupView.Arrange(rect);
    }

    /// <summary>
    /// Frames a bottom sheet against the CURRENT window: capped width, centered, bottom-anchored,
    /// with its detents resolved against the height available above the top inset.
    /// </summary>
    /// <param name="sheet">The presented sheet.</param>
    /// <param name="keyboardMode">How the sheet reacts to the soft keyboard.</param>
    /// <param name="controller">The scaffold controller, for the window insets.</param>
    /// <param name="container">The overlay container whose bounds the sheet is framed against.</param>
    /// <param name="keyboardOverlap">The keyboard overlap this sheet reacts to (0 unless it OWNS the keyboard — see <see cref="KeyboardOwner"/>).</param>
    /// <param name="initial">
    /// True on presentation (geometry is being established); false on a re-layout, where the sheet
    /// keeps the detent it rests on while its heights are re-derived for the new window.
    /// </param>
    private static void LayoutBottomSheet(ScaffoldBottomSheetView sheet, ScaffoldKeyboardMode keyboardMode, ScaffoldViewController controller, UIView container, bool initial, double keyboardOverlap)
    {
        var bounds = container.Bounds;
        var insets = controller.View!.SafeAreaInsets;

        var availableHeight = (double)(bounds.Height - insets.Top);

        // Resize: a visible keyboard is a bigger bottom inset — the sheet surface stays anchored
        // to the bottom edge (continuous behind the keyboard) while its content is padded up to
        // the keyboard's top edge (see ScaffoldOverlayGeometry). Pan / None: system inset only.
        var bottomPadding = keyboardMode == ScaffoldKeyboardMode.Resize
            ? ScaffoldOverlayGeometry.SheetBottomPadding(insets.Bottom, keyboardOverlap)
            : (double)insets.Bottom;

        // Padding first (it affects the natural height), then measure, then geometry.
        sheet.PrepareForMeasure(bottomPadding);

        var sheetView = (IView)sheet;
        var sheetWidth = Math.Min((double)bounds.Width, sheet.MaxWidth);
        var natural = sheetView.Measure(sheetWidth, availableHeight).Height;
        var naturalHeight = Math.Min(natural, availableHeight);

        var sheetHeight = initial
            ? sheet.InitializeGeometry(availableHeight, naturalHeight)
            : sheet.UpdateGeometry(availableHeight, naturalHeight);

        // Pan: the whole sheet slides up by the least that keeps the focused input above the
        // keyboard (its resting detent translation still applies on top of the frame).
        double pan = 0;

        if (keyboardMode == ScaffoldKeyboardMode.Pan && keyboardOverlap > 0)
        {
            var frameTop = (double)bounds.Height - sheetHeight;
            var visibleTop = frameTop + sheet.TranslationY;
            var focusedBottom = sheet.Handler?.PlatformView is UIView panel ? ScaffoldFocusedInput.BottomIn(panel) : null;

            pan = ScaffoldOverlayGeometry.Pan(
                bounds.Height - keyboardOverlap,
                visibleTop,
                bounds.Height,
                focusedBottom is { } inPanel ? visibleTop + inPanel : null,
                insets.Top
            );
        }

        // Bottom-anchored (minus the pan), centered at the (possibly capped) width; the sheet's
        // own TranslationY does the rest. Virtual arrange: a valid MAUI frame is required for
        // translations to apply.
        sheetView.Arrange(new Rect((bounds.Width - sheetWidth) / 2, bounds.Height - sheetHeight - pan, sheetWidth, sheetHeight));
    }

    /// <summary>
    /// The presented entry that OWNS the soft keyboard: the topmost sheet or popup — the keyboard
    /// inset is applied to that surface alone; when none is presented, the page owns it (see
    /// <see cref="ScaffoldViewController.OverlayOwnsKeyboard"/>).
    /// </summary>
    private OverlayEntry? KeyboardOwner
        => _overlays.LastOrDefault(static entry => !entry.Closing && entry.Request.Kind is ScaffoldOverlayKind.Popup or ScaffoldOverlayKind.BottomSheet);

    private double KeyboardOverlapFor(OverlayEntry entry, ScaffoldViewController controller)
        => ReferenceEquals(entry, KeyboardOwner) ? controller.KeyboardOverlap : 0;

    /// <summary>
    /// Re-places the overlays whose geometry depends on the soft keyboard (sheets, popups) after
    /// its overlap changed — or after the keyboard's OWNER changed (an entry presented or closed).
    /// Animated by design — see <see cref="ScaffoldViewController.KeyboardOverlapChanged"/>.
    /// </summary>
    private void RelayoutKeyboardAwareOverlays(ScaffoldViewController controller, UIView container)
    {
        foreach (var entry in _overlays.ToArray())
        {
            if (entry.Closing)
            {
                continue;
            }

            switch (entry.Request)
            {
                case { Content: ScaffoldBottomSheetView sheet }:
                    LayoutBottomSheet(sheet, entry.Request.KeyboardMode, controller, container, initial: false, KeyboardOverlapFor(entry, controller));

                    break;

                case { Kind: ScaffoldOverlayKind.Popup }:
                    LayoutPopup(entry.Request, controller, container, KeyboardOverlapFor(entry, controller));

                    break;
            }
        }
    }

    /// <summary>The keyboard's owner changed (an entry presented or closed): overlays and page re-apply their keyboard reaction.</summary>
    private void OnKeyboardOwnerChanged(ScaffoldViewController controller)
    {
        if (controller.KeyboardOverlap > 0)
        {
            RelayoutKeyboardAwareOverlays(controller, controller.ContentContainer);
        }

        controller.RefreshCurrentPageKeyboard();
    }

    /// <summary>
    /// Re-lays out presented overlays after the window changed shape (rotation, split view).
    /// </summary>
    /// <remarks>
    /// Overlay geometry is computed at presentation from the window of that moment. A window that
    /// changes shape — a rotation, but equally an iPad or tablet window the user simply drags to a
    /// new size — leaves a bottom sheet at its old width and old detent heights (off the side of
    /// the screen, taller than the window it now sits in) and a popup wherever it was centered or
    /// anchored, while their scrims, which autoresize, go on dimming everything.
    /// </remarks>
    /// <summary>
    /// Re-places the presented overlays for a window that changed shape.
    /// </summary>
    /// <remarks>
    /// UNANIMATED, and that is not cosmetic. This runs from the host's layout pass, and a rotation
    /// runs its layout passes inside UIKit's rotation animation block — where every frame written
    /// here would enrol in the running animation. Each enrolment makes UIKit walk the layer's
    /// accumulated animation list, so re-placing overlays pass after pass grows that list until the
    /// walk eats the main thread: the app freezes deep in _shouldAnimateAdditivelyForKey with the
    /// rotation transition on the stack. An overlay being re-placed for a new window should snap to
    /// it anyway — it is not a movement the user should watch.
    /// </remarks>
    private void RelayoutOverlays(ScaffoldViewController controller, UIView container)
        => UIView.PerformWithoutAnimation(() => RelayoutOverlaysCore(controller, container));

    private void RelayoutOverlaysCore(ScaffoldViewController controller, UIView container)
    {
        var closePanel = false;

        // Snapshot: closing the panel mutates the list.
        foreach (var entry in _overlays.ToArray())
        {
            if (entry.Closing)
            {
                continue;
            }

            switch (entry.Request)
            {
                case { Content: ScaffoldBottomSheetView sheet }:
                    LayoutBottomSheet(sheet, entry.Request.KeyboardMode, controller, container, initial: false, KeyboardOverlapFor(entry, controller));

                    break;

                case { Kind: ScaffoldOverlayKind.Popup }:
                    LayoutPopup(entry.Request, controller, container, KeyboardOverlapFor(entry, controller));

                    break;

                case { Kind: ScaffoldOverlayKind.Flyout }:
                    LayoutFlyout(entry.Request, container);

                    break;

                // The tab bar panel is DISMISSED by a shape change rather than re-laid out: it is
                // a transient menu hanging off the bar, and the set it lists is repartitioned for
                // the new window. Closing also settles what an open overflow panel should show
                // once a wider window has taken its items back — nothing, because it is gone.
                // The set-changed path (ScaffoldTabBar.OpenOverflowAsync) already closes it; this
                // covers the shape change that leaves the partition alone.
                case { Kind: ScaffoldOverlayKind.TabBarPanel }:
                    closePanel = true;

                    break;
            }
        }

        if (closePanel)
        {
            scaffold.CloseTabBarPanelAsync().FireAndForget(scaffold.Handler);
        }
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

        await previous.Content.FadeToAsync(0, 100);
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
        await replacement.Content.FadeToAsync(1, 100);
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

        if (request.Kind == ScaffoldOverlayKind.Flyout)
        {
            // The icons return to the underlying resolution as the flyout starts sliding away.
            scaffold.SystemBars.OverlaySurface = null;
        }

        await ScaffoldOverlayAnimations.ExitAsync(request, entry.ScrimView, entry.FlyoutOffscreenTranslation);

        // Owner cleanup (state flags, logical-child detach, handle completion) runs AFTER the
        // exit animation: detaching the content's logical child earlier freezes its exit
        // transforms — the sheet/popup would sit still and vanish at the end.
        request.Cleanup?.Invoke();

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

        // The keyboard goes back to the entry below, or to the page.
        if (request.Kind is ScaffoldOverlayKind.Popup or ScaffoldOverlayKind.BottomSheet
            && scaffold.Handler is IPlatformViewHandler { ViewController: ScaffoldViewController controller })
        {
            OnKeyboardOwnerChanged(controller);
        }

        entry.ClosedTcs.TrySetResult();
    }
}
