using System.ComponentModel;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using AndroidX.Fragment.App;
using Microsoft.Maui.Platform;
using Nalu.Internals;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using View = Microsoft.Maui.Controls.View;

namespace Nalu;

/// <summary>
/// Android presenter: hosts the visible page in a fragment (the MAUI Shell hosting model and the
/// base for predictive-back integration) inside the inset-rewriting page layer (§5.4), owns the
/// tab bar strip and the §5.6 overlay layer. Single-visible-page policy: one fragment replaced
/// per sync; the fragment back stack and the full transition engine arrive with P2.
/// </summary>
internal sealed class ScaffoldPresenter(Scaffold scaffold) : IScaffoldPresenter, IDisposable
{
    private const int _settleTimeoutMs = 2000;
    private const int _overlayDurationMs = 250;
    private const double _overflowGapDp = 8;

    private ScaffoldLayout? _hostPlatformView;
    private ScaffoldPageLayerLayout? _pageLayer;
    private FragmentContainerView? _container;
    private ScaffoldPageFragment? _currentFragment;
    private Page? _currentPage;
    private ScaffoldTabBarStripLayout? _tabBarStrip;
    private View? _currentBarView;
    private ScaffoldTabBar? _currentTabBarArea;
    private int _lastStripHeight;
    private bool _barPresented;
    private Android.Animation.ObjectAnimator? _stripAnimator;
    private ScaffoldNavBarStripLayout? _navBarStrip;
    private ScaffoldNavBarHost? _navBarHost;
    private ScaffoldArea? _observedNavBarArea;
    private bool _scaffoldObserved;
    private int _lastNavStripHeight;
    private Android.Animation.ObjectAnimator? _navStripAnimator;

    /// <summary>One presented §5.6 overlay entry: scrim + content, stacked in open order.</summary>
    private sealed class OverlayEntry
    {
        public required ScaffoldOverlayRequest Request { get; set; }
        public required View ScrimView { get; init; }
        public required AView ScrimPlatform { get; init; }
        public required AView ContentPlatform { get; set; }
        public double FlyoutOffscreenTranslation { get; init; }
        public bool Closing { get; set; }
        public TaskCompletionSource ClosedTcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public EventHandler? ContentMeasureInvalidated { get; set; }
        public bool ContentRelayoutPending { get; set; }
    }

    private readonly List<OverlayEntry> _overlays = [];

    /// <summary>
    /// Sheets and popups are placed from their content's NATURAL size, which can change after
    /// presentation (a deferred image, an expanding section, a loaded list): the content's
    /// measure invalidation (bubbling up from any descendant) schedules one re-placement on the
    /// next dispatcher turn — the popup re-fits and re-centers/re-anchors, a Content-detent sheet
    /// re-resolves its height. Coalesced; the arrange itself does not invalidate, so it converges.
    /// </summary>
    private void ObserveContentMeasure(OverlayEntry entry, ScaffoldLayout container, Android.Content.Context context)
    {
        entry.ContentMeasureInvalidated = (_, _) =>
        {
            if (entry.Closing || entry.ContentRelayoutPending)
            {
                return;
            }

            entry.ContentRelayoutPending = true;

            scaffold.Dispatcher.Dispatch(() =>
            {
                // The flag stays up while re-placing: the pass itself invalidates (the sheet
                // toggles its content clamp around the measure) and must not re-schedule.
                try
                {
                    if (entry.Closing || !_overlays.Contains(entry) || entry.ContentPlatform is not { } panel)
                    {
                        return;
                    }

                    switch (entry.Request)
                    {
                        case { Content: ScaffoldBottomSheetView sheet }:
                            panel.LayoutParameters = LayoutBottomSheet(sheet, entry.Request.KeyboardMode, panel, container, context, initial: false, KeyboardOverlapFor(entry, container));

                            break;

                        case { Kind: ScaffoldOverlayKind.Popup }:
                            panel.LayoutParameters = LayoutPopup(entry.Request, container, context, KeyboardOverlapFor(entry, container));

                            break;
                    }
                }
                finally
                {
                    entry.ContentRelayoutPending = false;
                }
            });
        };

        entry.Request.Content.MeasureInvalidated += entry.ContentMeasureInvalidated;
    }

    private ScaffoldRoot? _currentRoot;
    private AView? _backPeekView;
    private AView? _backTopView;
    private Page? _backBelowPage;
    private bool _backPreviewActive;
    private Page? _predictiveHandoffPage;
    private bool _previewOwnsTransitionFlag;

    /// <summary>One long-lived listener instance for every frozen host (it references no page).</summary>
    private static readonly FrozenInsetsListener _frozenInsetsListener = new();

    /// <summary>Consumes insets so a frozen host's subtree keeps its stale (correct) layout.</summary>
    private sealed class FrozenInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
            => WindowInsetsCompat.Consumed;
    }
    private ScaffoldFlightSession? _backFlightSession;
    private float _backProgress;
    private LeavingPage? _leavingPage;

    public bool HasOverlay => _overlays.Count > 0;

    public bool IsOverlayPresented(ScaffoldOverlayRequest request) => FindEntry(request) is not null;

    private OverlayEntry? FindEntry(ScaffoldOverlayRequest request)
        => _overlays.Find(entry => ReferenceEquals(entry.Request, request));

    /// <summary>Whether a predictive-back preview is currently scrubbing.</summary>
    public bool HasBackPreview => _backPreviewActive;

    public async Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
    {
        if (scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext } ||
            platformView.Context?.GetActivity() is not AppCompatActivity activity)
        {
            return;
        }

        scaffold.EnsureBackCallback(activity);

        // The presented root is the back preview's source of truth for the stack.
        _currentRoot = root;

        // A navigation arriving while a back gesture is still scrubbing (programmatic push,
        // tab selection) invalidates the preview; a handoff sync is the preview's OWN commit.
        if (_backPreviewActive && _predictiveHandoffPage is null)
        {
            AbortBackPreview();
        }

        // Navigation dismisses any open overlay (flyout, overflow panel).
        await CloseAllOverlaysAsync();

        var container = EnsureContainer(platformView);
        // Presented overlays keep the geometry of the window they were shown in: re-lay them out
        // when it changes shape.
        platformView.WindowGeometryChanged ??= () => RelayoutOverlays(platformView, platformView.Context!);

        // ...and when the soft keyboard changes its overlap (per animation frame while it moves):
        // sheets and popups are re-placed against the area ABOVE it.
        platformView.KeyboardInsetsChanged ??= () =>
        {
            scaffold.KeyboardState.Update(platformView.Context!.FromPixels(platformView.ImeBottomInsetPx));
            RelayoutKeyboardAwareOverlays(platformView, platformView.Context!);
        };
        platformView.OverlayOwnsKeyboard ??= () => KeyboardOwner is not null;

        var stack = root.NavigationStack;
        var targetPage = stack.PushedPages.Count > 0 ? stack.PushedPages[^1].Page : stack.RootPage;

        if (targetPage is null)
        {
            scaffold.UpdateBackCallbackEnabled();

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

        // Inset intent BEFORE the fragment commit: the incoming page attaches with its final
        // insets while the outgoing page keeps its stale layout — no jumps during transitions.
        // "Keeps its stale layout" is ENFORCED, not hoped for: the outgoing host's insets
        // dispatch is frozen first (a consuming listener — API 30+, where consumption stops
        // the subtree without starving siblings), because any pass landing mid-transition
        // would re-pad the covered page with the INCOMING page's chrome intent (differing
        // intents — e.g. a nav-bar page pushing an overlap page — made its content jump and
        // corrupted its scroll position for the eventual pop). One-way per host: every mount
        // builds a fresh host and re-applies the page's own insets.
        // (NOT a managed View subclass on purpose: a managed Java peer in the host chain
        // defers the GC-bridge release of popped pages past the leak detector's patience.)
        if (OperatingSystem.IsAndroidVersionAtLeast(30)
            && !ReferenceEquals(targetPage, _currentPage)
            && _currentFragment?.View is { } outgoingHost)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(outgoingHost, _frozenInsetsListener);
        }

        platformView.ChromeBottomDesired = barVisible;
        platformView.PageBottomInsetPx = barVisible ? _lastStripHeight : 0;
        platformView.ChromeTopDesired = navBarInsets;
        platformView.PageTopInsetPx = navBarInsets ? _lastNavStripHeight : 0;
        platformView.PageKeyboardMode ??= () => scaffold.ResolvePageKeyboardMode(_currentPage);

        // Chrome and page animate CONCURRENTLY: an Auto-hiding bar slides away while the pushed
        // page slides in (and back in sync on pop).
        // Nav bar first: its strip must sit BELOW the tab bar strip in z-order (behind-chrome
        // overlay scrims dim the nav bar while keeping the tab bar interactive).
        var navChromeTask = UpdateNavBarChromeAsync(platformView, mauiContext, targetPage, navBarView, navBarVisible, animated);
        var chromeTask = UpdateTabBarChromeAsync(platformView, mauiContext, tabBarArea, barVisible, animated);

        if (!ReferenceEquals(targetPage, _currentPage))
        {
            var previousPage = _currentPage;

            if (previousPage is not null)
            {
                previousPage.PropertyChanged -= OnCurrentPagePropertyChanged;

                // BEFORE the fragment commit: Android never dismisses the IME when the focused
                // hierarchy is torn down — navigating away with the keyboard open would orphan
                // it over the incoming page.
                // KNOWN LIMITATION (net10 MAUI): if the commit lands while the IME hide is still
                // animating, MauiWindowInsetListener swallows insets dispatches until the
                // animation ends (IsImeAnimating gate) and the incoming page briefly shows with
                // stale safe-area padding before snapping into place. Deliberately not worked
                // around here — it would require polling MAUI internals via reflection.
                HideSoftInputBeforeNavigation(previousPage);
            }

            // A committed predictive-back preview already settled the visuals (top page
            // offscreen, below page fully revealed via the peek): adopt without re-animating —
            // no shared-element transition, no exit animator on the removed fragment.
            var handoffPage = _predictiveHandoffPage;
            _predictiveHandoffPage = null;
            var predictivelySettled = handoffPage is not null
                && hint == ScaffoldPresentationHint.Pop
                && ReferenceEquals(handoffPage, targetPage);

            // Shared elements (§8): matching Scaffold.TransitionName pairs between the two
            // pages fly in OUR overlay engine (ScaffoldSharedElementTransitions) — the native
            // androidx TransitionSet cannot animate corner radii, text scale or cross-fades
            // and cannot be extended (managed Transition subclasses lose their peer on clone).
            var sharedNames = !predictivelySettled && previousPage is not null && animated
                ? ScaffoldTransitions.MatchingNames(ScaffoldTransitions.Collect(previousPage), ScaffoldTransitions.Collect(targetPage))
                : [];

            // §8.2 spec resolution: the spec belongs to the PUSHED page — it enters with it
            // (push) and leaves with it reversed (pop reveals with its Behind reversed).
            // Shared-element navigations keep the standard slide (the SET choreography
            // assumes it), so they run with the Default spec.
            var pageTransition = sharedNames.Count > 0
                ? ScaffoldPageTransition.Default
                : hint switch
                {
                    ScaffoldPresentationHint.Push => scaffold.ResolvePageTransition(targetPage),
                    ScaffoldPresentationHint.Pop when previousPage is not null => scaffold.ResolvePageTransition(previousPage),
                    _ => scaffold.ResolveRootSwitchTransition()
                };

            var previousFragment = _currentFragment;
            var previousView = previousFragment?.View;

            // What the OUTGOING page does while the incoming one arrives — null when it simply
            // goes away (no previous page, an unanimated swap, or a predictive-back preview that
            // already moved it offscreen). Note that an IDENTITY motion is not nothing: a push
            // leaves the covered page exactly where it is, and it must stay VISIBLE there.
            var leavingMotion = previousView is null || predictivelySettled || !pageTransition.IsAnimated
                ? null
                : hint switch
                {
                    // The covered page plays the incoming spec's Behind motion (identity by
                    // default: it stays at rest under the page sliding over it).
                    ScaffoldPresentationHint.Push => (ScaffoldTransitionMotion?)pageTransition.Behind,

                    // The popped page replays its OWN Enter motion in reverse.
                    ScaffoldPresentationHint.Pop => pageTransition.Enter,

                    // Root switch within an area: both pages travel together, same direction.
                    ScaffoldPresentationHint.SlideStart => new ScaffoldTransitionMotion(FractionX: 1),
                    ScaffoldPresentationHint.SlideEnd => new ScaffoldTransitionMotion(FractionX: -1),

                    // Cross-area switch: the outgoing root fades out over the new one (a
                    // symmetric double fade would show the window through both at the midpoint).
                    ScaffoldPresentationHint.Fade => new ScaffoldTransitionMotion(Opacity: 0),
                    _ => null
                };

            // Motions that play ABOVE the incoming page. Safe even with shared elements: their
            // flights live in the container's OVERLAY, which the framework draws after every
            // child (View.draw runs it past dispatchDraw, outside the enableZ/disableZ span),
            // so an elevated page cannot cover them.
            var leavingLeads = hint is ScaffoldPresentationHint.Pop or ScaffoldPresentationHint.Fade;

            // A settled predictive-back preview already placed the revealed page at rest: the
            // incoming fragment must not replay the reveal motion on top of it.
            var enterTransition = predictivelySettled ? ScaffoldPageTransition.None : pageTransition;
            var fragment = new ScaffoldPageFragment(mauiContext, targetPage, hint, container, enterTransition);

            // Depth cues for STACKED motions only (side-by-side root switches get neither):
            // a push slides the incoming page ABOVE with a shadow while the covered page dims;
            // a pop reveals the incoming page dimmed, brightening as the leaving page departs
            // with its own shadow (see PrepareLeavingPage).
            var depthPush = hint == ScaffoldPresentationHint.Push && enterTransition.IsAnimated;
            var depthPop = hint == ScaffoldPresentationHint.Pop && !predictivelySettled && animated;

            // The page must see the chrome-rewritten insets (nav bar / tab bar footprints) before
            // its first layout: the window dispatches insets only when they CHANGE, so a page
            // mounted in between lays out against the raw system bars and slides its content
            // under the nav bar strip.
            fragment.OnViewMounted = view =>
            {
                _pageLayer!.ApplyInsetsTo(view);

                if (depthPush)
                {
                    ScaffoldPageDepth.ApplyShadow(view);
                }

                if (depthPop)
                {
                    ScaffoldPageDepth.SetDim(view, 1f);
                }
            };
            _currentFragment = fragment;
            _currentPage = targetPage;
            targetPage.PropertyChanged += OnCurrentPagePropertyChanged;

            // MAUI page lifecycle: Disappearing on the covered page, Appearing on the incoming one —
            // raised BEFORE the navigation events, matching the order MAUI's own hosts use.
            ScaffoldPageNavigationEvents.SendAppearanceChange(previousPage, targetPage);

            // MAUI page navigation events: features like HideSoftInputOnTapped are gated on
            // Page.HasNavigatedTo, which only these raise.
            ScaffoldPageNavigationEvents.SendNavigated(previousPage, targetPage, hint.ToNavigationType());

            // Async commit only: a synchronous commit can run while MAUI's own ScopedFragment
            // transaction is still executing on the same FragmentManager ("already executing").
            var transaction = activity.SupportFragmentManager
                                      .BeginTransaction()
                                      .SetReorderingAllowed(true);

            if (sharedNames.Count > 0)
            {
                // Source side captured NOW (the outgoing page is still at rest); the flights
                // start at the incoming fragment's first pre-draw — the destination geometry
                // exists exactly then, and it is the same frame that page becomes visible.
                var flightSession = ScaffoldSharedElementTransitions.Prepare(
                    mauiContext,
                    container,
                    previousPage!,
                    targetPage,
                    sharedNames,
                    ScaffoldTransitions.Collect(previousPage!),
                    pageTransition.DurationSeconds);

                if (flightSession is not null)
                {
                    fragment.OnFirstPreDraw = flightSession.Start;
                }
            }

            // At most one page leaves at a time: a navigation landing mid-motion unmounts the
            // previous one instantly (its page is already out of the model).
            FinishLeavingPage();

            // A page that has to stay on screen while it leaves keeps its fragment ADDED: a
            // Replace destroys the outgoing view the instant the transaction executes — the
            // fragment machinery removes a REMOVED operation's view from whatever parent it
            // has (SpecialEffectsController.Operation.State), so neither a fragment exit
            // animation (drawn UNDER the entering page by FragmentContainerView) nor hoisting
            // the view elsewhere can hold it. It is removed by StartLeavingPageAsync instead,
            // once its motion ends.
            if (leavingMotion is not null)
            {
                transaction.Add(container.Id, fragment);
            }
            else
            {
                transaction.Replace(container.Id, fragment);
            }

            transaction.CommitAllowingStateLoss();

            // Pages in motion take no input (restored in the finally even if the settle times
            // out — a layer stuck deaf would be far worse than a stray tap).
            var pageLayer = _pageLayer;

            if (pageLayer is not null)
            {
                pageLayer.TransitionInFlight = animated;

                // The sync owns the flag from here (its finally below clears it): a pending
                // preview-cancel animator must not clear it mid-transition. Any per-peek inset
                // override ends too — the layer-wide intent set above describes the incoming
                // page now (an adopted peek was precomputed with exactly those values).
                _previewOwnsTransitionFlag = false;
                pageLayer.ClearPeekInsetIntent();
            }

            try
            {
                var leaving = leavingMotion is null
                    ? null
                    : PrepareLeavingPage(activity, previousFragment!, previousPage!, previousView!, leavingMotion, leavingLeads, pageTransition);

                if (leaving is not null)
                {
                    // WEAK captures only, in EVERY animator Update handler: a strong capture in
                    // an update-listener closure roots the captured view (and its page and
                    // model) past the animator's life — a GC-bridge peculiarity the leak
                    // suites catch (End handlers do not exhibit it; Update handlers do).
                    var weakFragment = new WeakReference<ScaffoldPageFragment>(fragment);
                    var weakLeavingView = new WeakReference<AView>(previousView!);

                    if (depthPop)
                    {
                        // The popped page departs with a shadow; the revealed one brightens as it goes.
                        ScaffoldPageDepth.ApplyShadow(previousView!);

                        leaving.Animator.Update += (_, args) =>
                        {
                            if (weakFragment.TryGetTarget(out var incoming) && incoming.View is { } revealedView)
                            {
                                ScaffoldPageDepth.SetDim(revealedView, 1f - args.Animation.AnimatedFraction);
                            }
                        };
                    }
                    else if (depthPush)
                    {
                        // The covered page dims under the incoming one.
                        leaving.Animator.Update += (_, args) =>
                        {
                            if (weakLeavingView.TryGetTarget(out var coveredView))
                            {
                                ScaffoldPageDepth.SetDim(coveredView, args.Animation.AnimatedFraction);
                            }
                        };
                    }
                }

                // Started at the INCOMING page's first pre-draw — the frame it is laid out and
                // about to render — not here. The incoming half is started by the fragment
                // machinery when the transaction executes, a frame after this commit, so starting
                // the outgoing half now runs it a frame ahead: with matched easing the error
                // tracks velocity, which peaks mid-transition, and the window shows through the
                // seam between the pages (measured at ~115px of a 1080px slide). Waiting for the
                // pre-draw also means a page that is slow to lay out delays BOTH halves.
                var startFlights = fragment.OnFirstPreDraw;

                fragment.OnFirstPreDraw = () =>
                {
                    startFlights?.Invoke();
                    leaving?.Animator.Start();
                };

#pragma warning disable VSTHRD003
                var leavingTask = leaving?.Completion.Task ?? Task.CompletedTask;
#pragma warning restore VSTHRD003

                // Deterministic completion: presentation of the new page plus the outgoing page's
                // motion, with a settle timeout as a safety net.
                var settled = Task.WhenAll(fragment.PresentedTask, leavingTask);
                await Task.WhenAny(settled, Task.Delay(_settleTimeoutMs));
            }
            finally
            {
                if (pageLayer is not null)
                {
                    pageLayer.TransitionInFlight = false;
                }

                // Depth cues end with the transition: the presented page keeps no shadow and no
                // dim; the covered/departed page (about to unmount, kept alive) no residual dim.
                if (fragment.View is { } presentedView)
                {
                    ScaffoldPageDepth.ClearShadow(presentedView);
                    ScaffoldPageDepth.SetDim(presentedView, 0f);
                }

                if (previousView is not null)
                {
                    ScaffoldPageDepth.SetDim(previousView, 0f);
                }
            }
        }

        await Task.WhenAll(navChromeTask, chromeTask);

        scaffold.UpdateBackCallbackEnabled();

        // Presentation at rest: the pixels under the status bar are final — read fresh.
        scaffold.SystemBars.OnPresentationSettled();
    }

    // 1:1 with BackEvent progress: the page follows the finger (iOS parity), revealing exactly
    // what the gesture has earned; the commit settle completes whatever remains.
    private const float _backPreviewMaxShift = 1f;

    /// <summary>
    /// Predictive back, gesture started: peek-mounts the page below (presentation-only, no
    /// lifecycle — the engine still owns the stack) beneath the fragment container so the
    /// scrubbed page reveals it. Guarded pages (<see cref="ILeavingGuard"/>) get NO preview —
    /// the committed back still routes through the engine, which runs the guard.
    /// </summary>
    public void StartBackPreview()
    {
        if (_backPreviewActive
            || HasOverlay
            || _pageLayer is not { } pageLayer
            || _container is not { } container
            || scaffold.Handler is not IPlatformViewHandler { MauiContext: { } mauiContext }
            || _currentRoot?.NavigationStack is not { PushedPages.Count: > 0 } stack
            || stack.PushedPages[^1].Page is not { } topPage
            || NavigationHelper.GetLifecycleTarget(topPage) is ILeavingGuard
            || Scaffold.GetPageMode(topPage) != ScaffoldPageMode.Default
            || !ReferenceEquals(topPage, _currentPage)
            || _currentFragment?.View is not { } topView)
        {
            return;
        }

        var belowPage = stack.PushedPages.Count > 1 ? stack.PushedPages[^2].Page : stack.RootPage;

        if (belowPage is null)
        {
            return;
        }

        var belowView = belowPage.ToPlatform(mauiContext);
        (belowView.Parent as AViewGroup)?.RemoveView(belowView);
        belowView.TranslationX = 0f;
        belowView.TranslationZ = 0f;
        pageLayer.AddView(belowView, 0, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));

        // The preview spans a transition for the inset machinery too: the flag parks the
        // scrubbed transforms for the span of any insets dispatch (so nothing gets padded for
        // where it momentarily sits) and blocks input on pages in motion. The commit's sync
        // (or the cancel path below) restores it.
        pageLayer.TransitionInFlight = true;
        _previewOwnsTransitionFlag = true;

        // The peek is padded for where it will LAND, not for the top page's chrome: the
        // layer-wide inset intent still belongs to the scrubbed page for the whole gesture
        // (its nav bar may overlap content while the revealed page shows one, its tab bar may
        // be hidden while the revealed root brings it back), so the peek registers its OWN
        // intent, computed exactly like the sync will compute it on commit.
        var belowTabVisible = _currentRoot.Parent is ScaffoldTabBar && Scaffold.ComputeTabBarVisible(_currentRoot, belowPage);
        var belowNavBarVisible = scaffold.ResolveNavBarView(belowPage) is not null && Scaffold.GetIsNavBarVisible(belowPage);
        var belowNavBarInsets = belowNavBarVisible && !Scaffold.GetNavBarOverlapsContent(belowPage);

        pageLayer.SetPeekInsetIntent(
            belowView,
            belowNavBarInsets ? _lastNavStripHeight : 0,
            belowTabVisible ? _lastStripHeight : 0
        );

        // A freshly mounted view gets no insets dispatch of its own (the window only
        // re-dispatches when the insets CHANGE): without this the peeked page lays out
        // edge-to-edge — content under the status bar — and jumps into place when the pop
        // commits and a real dispatch finally lands.
        pageLayer.ApplyInsetsTo(belowView);

        // Depth cues: the scrubbed page casts a shadow so its boundary reads against the peek,
        // which starts fully dimmed and brightens as the page departs.
        ScaffoldPageDepth.ApplyShadow(topView);
        ScaffoldPageDepth.SetDim(belowView, 1f);

        _backPeekView = belowView;
        _backTopView = topView;
        _backBelowPage = belowPage;
        _backPreviewActive = true;
        _backProgress = 0f;

        // Shared elements fly DURING the gesture: matching pairs between the scrubbed page
        // (source, captured at rest — that is now) and the peek (destination) are built as a
        // SEEKABLE session the scrub drives — the same flights a committed pop would play,
        // driven by the finger instead of an animator.
        var topTagged = ScaffoldTransitions.Collect(topPage);
        var belowTagged = ScaffoldTransitions.Collect(belowPage);
        var sharedNames = ScaffoldTransitions.MatchingNames(topTagged, belowTagged);

        if (sharedNames.Count > 0
            && ScaffoldSharedElementTransitions.Prepare(
                mauiContext,
                container,
                topPage,
                belowPage,
                sharedNames,
                topTagged,
                scaffold.ResolvePageTransition(topPage).DurationSeconds
            ) is { } flightSession)
        {
            _backFlightSession = flightSession;

            if (belowView.Width > 0)
            {
                // A previously-presented page kept its layout: destination geometry is valid now.
                flightSession.TryBuild();
                flightSession.Seek(0f, 0f);
            }
            else
            {
                // Fresh platform view: destination geometry exists at its first pre-draw.
                OneShotPreDrawListener.Add(
                    belowView,
                    new Java.Lang.Runnable(() =>
                        {
                            if (ReferenceEquals(_backFlightSession, flightSession) && flightSession.TryBuild())
                            {
                                flightSession.Seek(_backProgress, topView.TranslationX);
                            }
                        }
                    )
                );
            }
        }
    }

    /// <summary>Predictive back, gesture progressing: page motion + shared-element flights, finger-driven.</summary>
    public void UpdateBackPreview(float progress)
    {
        if (_backPreviewActive && _backTopView is { } topView)
        {
            _backProgress = progress;
            topView.TranslationX = progress * _backPreviewMaxShift * (topView.Width > 0 ? topView.Width : 0);
            _backFlightSession?.Seek(progress, topView.TranslationX);

            if (_backPeekView is { } peekView)
            {
                ScaffoldPageDepth.SetDim(peekView, 1f - progress);
            }
        }
    }

    /// <summary>Predictive back, gesture cancelled: the top page slides home, peek unmounts.</summary>
    public void CancelBackPreview()
    {
        if (!_backPreviewActive)
        {
            return;
        }

        _backPreviewActive = false;
        var topView = _backTopView;
        var peekView = _backPeekView;
        var flightSession = _backFlightSession;
        _backTopView = null;
        _backPeekView = null;
        _backBelowPage = null;
        _backFlightSession = null;

        if (topView is null)
        {
            flightSession?.Finish();
            TearDownPeekDepth(topView: null, peekView);
            RemovePeek(peekView);
            ClearPreviewTransitionFlag();

            return;
        }

        var startProgress = _backProgress;
        var animator = Android.Animation.ObjectAnimator.OfFloat(topView, "translationX", topView.TranslationX, 0f)!;
        animator.SetDuration(150);

        // The flights ride home with the page; the peek dims back toward covered.
        // WEAK captures in the Update handler (see the sync's leaving-animator note).
        var weakCancelTop = new WeakReference<AView>(topView);
        var weakCancelPeek = peekView is null ? null : new WeakReference<AView>(peekView);
        var weakCancelSession = flightSession is null ? null : new WeakReference<ScaffoldFlightSession>(flightSession);

        animator.Update += (_, args) =>
        {
            if (!weakCancelTop.TryGetTarget(out var top))
            {
                return;
            }

            var flightProgress = startProgress * (1 - args.Animation.AnimatedFraction);

            if (weakCancelSession is not null && weakCancelSession.TryGetTarget(out var session))
            {
                session.Seek(flightProgress, top.TranslationX);
            }

            if (weakCancelPeek is not null && weakCancelPeek.TryGetTarget(out var peek))
            {
                ScaffoldPageDepth.SetDim(peek, 1f - flightProgress);
            }
        };

        animator.AnimationEnd += (_, _) =>
        {
            // Severed explicitly: defense in depth alongside the weak captures.
            animator.RemoveAllUpdateListeners();
            flightSession?.Finish();
            TearDownPeekDepth(topView, peekView);
            RemovePeek(peekView);

            // Clearing re-dispatches: the pages recompute their padding at rest.
            ClearPreviewTransitionFlag();
        };

        animator.Start();
    }

    /// <summary>Ends the preview's depth cues: the staying page loses its shadow, the peek its dim.</summary>
    private static void TearDownPeekDepth(AView? topView, AView? peekView)
    {
        if (topView is not null)
        {
            ScaffoldPageDepth.ClearShadow(topView);
        }

        if (peekView is not null)
        {
            ScaffoldPageDepth.SetDim(peekView, 0f);
        }
    }

    /// <summary>
    /// Ends the preview's transition span — a no-op once a sync has taken the flag over
    /// (its own finally clears it when the presentation settles), so a late animator-end
    /// callback can never re-dispatch insets in the middle of someone else's transition.
    /// </summary>
    private void ClearPreviewTransitionFlag()
    {
        if (_previewOwnsTransitionFlag)
        {
            _previewOwnsTransitionFlag = false;

            if (_pageLayer is { } pageLayer)
            {
                pageLayer.TransitionInFlight = false;
            }
        }
    }

    /// <summary>
    /// Predictive back, gesture committed: the top page settles fully offscreen FIRST (the
    /// gesture's motion completes uninterrupted), then the pop is dispatched through the
    /// engine; the sync it triggers adopts the settled state via the handoff (no exit animator,
    /// no shared-element transition). If the engine refuses (busy, or a guard surfaced), the
    /// preview reverses.
    /// </summary>
    public async Task CommitBackPreviewAsync()
    {
        if (!_backPreviewActive)
        {
            return;
        }

        _backPreviewActive = false;
        var topView = _backTopView;
        var peekView = _backPeekView;
        var belowPage = _backBelowPage;
        var flightSession = _backFlightSession;
        _backTopView = null;
        _backPeekView = null;
        _backBelowPage = null;
        _backFlightSession = null;

        if (topView is null || belowPage is null)
        {
            flightSession?.Finish();
            TearDownPeekDepth(topView, peekView);
            RemovePeek(peekView);

            return;
        }

        var startProgress = _backProgress;
        var width = topView.Width > 0 ? topView.Width : 1;
        var settle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animator = Android.Animation.ObjectAnimator.OfFloat(topView, "translationX", topView.TranslationX, width)!;
        animator.SetDuration((long)(_overlayDurationMs * (1 - (topView.TranslationX / width))));

        // The flights complete their remaining path while the page settles offscreen and the
        // peek finishes brightening.
        // WEAK captures in the Update handler (see the sync's leaving-animator note): the top
        // page is about to POP — a strong capture here roots it, its page and model forever.
        var weakCommitTop = new WeakReference<AView>(topView);
        var weakCommitPeek = peekView is null ? null : new WeakReference<AView>(peekView);
        var weakCommitSession = flightSession is null ? null : new WeakReference<ScaffoldFlightSession>(flightSession);

        animator.Update += (_, args) =>
        {
            if (!weakCommitTop.TryGetTarget(out var top))
            {
                return;
            }

            var flightProgress = startProgress + ((1 - startProgress) * args.Animation.AnimatedFraction);

            if (weakCommitSession is not null && weakCommitSession.TryGetTarget(out var session))
            {
                session.Seek(flightProgress, top.TranslationX);
            }

            if (weakCommitPeek is not null && weakCommitPeek.TryGetTarget(out var peek))
            {
                ScaffoldPageDepth.SetDim(peek, 1f - flightProgress);
            }
        };

        animator.AnimationEnd += (_, _) => settle.TrySetResult();
        animator.Start();
        await settle.Task;

        // Severed explicitly: a lingering Java-side update-listener chain would root the
        // closure — and the departed page's view, page and model with it.
        animator.RemoveAllUpdateListeners();

        // Flights are AT their destination — the live views they hand back to sit exactly there.
        flightSession?.Finish();

        // The peek is about to become the presented page: no residual dim (the departed page's
        // shadow leaves with it; remounts reset elevation).
        TearDownPeekDepth(topView, peekView);

        _predictiveHandoffPage = belowPage;

        var popped = scaffold.NavigationService is { } navigationService
            && await navigationService.GoToAsync(Navigation.Relative().Pop());

        if (!popped && ReferenceEquals(_predictiveHandoffPage, belowPage))
        {
            // Engine refused: restore the pre-gesture presentation.
            _predictiveHandoffPage = null;

            // The staying page needs its shadow back for the return slide.
            ScaffoldPageDepth.ApplyShadow(topView);

            var restore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var restoreAnimator = Android.Animation.ObjectAnimator.OfFloat(topView, "translationX", topView.TranslationX, 0f)!;
            restoreAnimator.SetDuration(_overlayDurationMs);
            restoreAnimator.AnimationEnd += (_, _) => restore.TrySetResult();
            restoreAnimator.Start();
            await restore.Task;
            TearDownPeekDepth(topView, peekView);
            RemovePeek(peekView);
            ClearPreviewTransitionFlag();
        }
    }

    /// <summary>Instant teardown for a preview invalidated by an unrelated navigation.</summary>
    private void AbortBackPreview()
    {
        _backPreviewActive = false;

        if (_backTopView is { } topView)
        {
            topView.TranslationX = 0f;
        }

        _backFlightSession?.Finish();
        _backFlightSession = null;
        TearDownPeekDepth(_backTopView, _backPeekView);
        RemovePeek(_backPeekView);
        _backTopView = null;
        _backPeekView = null;
        _backBelowPage = null;

        // Everything is back at rest; the invalidating sync (re)takes the flag if it animates.
        ClearPreviewTransitionFlag();
    }

    /// <summary>A page still mounted in the fragment container while it plays its leaving motion.</summary>
    private sealed class LeavingPage
    {
        public required ScaffoldPageFragment Fragment { get; init; }
        public required Page Page { get; init; }
        public required FragmentManager FragmentManager { get; init; }
        public required AView View { get; init; }
        public required ImportantForAccessibility Accessibility { get; init; }
        public required Android.Animation.ObjectAnimator Animator { get; init; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Prepares the OUTGOING page's motion, played IN PLACE — its fragment is still added, so its view is
    /// still a plain child of the container — and unmounts it when the motion ends.
    /// The fragment's own exit animations cannot serve here:
    /// <see cref="FragmentContainerView"/> deliberately draws exiting fragment views BELOW the
    /// entering one (a popped page would slide away hidden underneath the very page it reveals),
    /// and the fragment machinery removes a REMOVED operation's view from whatever parent it has
    /// — so a page with no exit animation blanks out the instant the transaction executes
    /// (flashing the window background through a push) and hoisting the view into another layer
    /// does not save it either.
    /// <paramref name="leads"/> raises the page above the incoming one for the motions that must
    /// play on top (pop, cross-area fade): translationZ rather than child order, because the
    /// incoming view is appended to the container a frame from now.
    /// </summary>
    private LeavingPage PrepareLeavingPage(
        AppCompatActivity activity,
        ScaffoldPageFragment leavingFragment,
        Page leavingPage,
        AView leavingView,
        ScaffoldTransitionMotion motion,
        bool leads,
        ScaffoldPageTransition transition)
    {
        if (leads)
        {
            // Above the incoming page; a POP's elevation is the real depth-cue shadow (the
            // caller adds it via ScaffoldPageDepth), this is just the stacking floor.
            leavingView.TranslationZ = Math.Max(leavingView.TranslationZ, 1f);
        }

        // A page in flight is no longer a destination: screen readers must land on the incoming
        // one, not on the content sliding away (input is blocked for everyone meanwhile —
        // ScaffoldPageLayerLayout.TransitionInFlight).
        var accessibility = leavingView.ImportantForAccessibility;
        leavingView.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;

        var width = leavingView.Width;
        var height = leavingView.Height;

        var animator = Android.Animation.ObjectAnimator.OfPropertyValuesHolder(
            leavingView,
            Android.Animation.PropertyValuesHolder.OfFloat("translationX", 0f, (float) (motion.FractionX * width))!,
            Android.Animation.PropertyValuesHolder.OfFloat("translationY", 0f, (float) (motion.FractionY * height))!,
            Android.Animation.PropertyValuesHolder.OfFloat("scaleX", 1f, (float) motion.Scale)!,
            Android.Animation.PropertyValuesHolder.OfFloat("scaleY", 1f, (float) motion.Scale)!,
            Android.Animation.PropertyValuesHolder.OfFloat("alpha", 1f, (float) motion.Opacity)!
        );

        animator.SetDuration((long) (transition.DurationSeconds * 1000));

        var session = new LeavingPage
                      {
                          Fragment = leavingFragment,
                          Page = leavingPage,
                          FragmentManager = activity.SupportFragmentManager,
                          View = leavingView,
                          Accessibility = accessibility,
                          Animator = animator
                      };

        _leavingPage = session;

        animator.AnimationEnd += (_, _) =>
        {
            if (ReferenceEquals(_leavingPage, session))
            {
                FinishLeavingPage();
            }
        };

        // NOT started here — the caller starts it in the same frame as the incoming page's own
        // animator, so the two halves of a transition stay locked together.
        return session;
    }

    /// <summary>
    /// Settles any in-flight leaving motion NOW and unmounts the page that was playing it.
    /// The view's motion state is deliberately NOT reset here: the removal transaction only
    /// executes on the next looper pass, and snapping the page back to its resting place first
    /// would flash it. Every mount clears it (<see cref="ScaffoldPageFragment.OnCreateView"/>).
    /// </summary>
    private void FinishLeavingPage()
    {
        if (_leavingPage is not { } leaving)
        {
            return;
        }

        // Cleared FIRST: Cancel() raises AnimationEnd, which must find nothing left to do.
        _leavingPage = null;
        leaving.Animator.Cancel();

        // Severed EXPLICITLY: a Java-side update-listener chain outlives the managed wrapper
        // and roots its closure — which captures the popped page's view — leaking the page and
        // its model (caught by the leak-detector suites).
        leaving.Animator.RemoveAllUpdateListeners();
        leaving.Completion.TrySetResult();

        // Restoring accessibility costs nothing visually (unlike the motion state, left alone
        // above) and the page keeps the flag across mounts otherwise.
        leaving.View.ImportantForAccessibility = leaving.Accessibility;

        // A teardown path (handler disconnect during activity destruction) reaches this with a
        // dead FragmentManager, which throws on ANY commit — state-loss variants included.
        if (!leaving.Fragment.IsAdded || leaving.FragmentManager.IsDestroyed)
        {
            return;
        }

        // Unmounted HERE, host AND page. A page that has left must end up exactly as a Replace
        // used to leave it — its platform view PARENTLESS — because that is what "not presented"
        // means to everything that walks the tree: a page still hosted inside a detached host
        // keeps reporting geometry, so it reads as displayed while it is nowhere on screen.
        // Detaching the page view first also hands it over safely: the FragmentManager executes
        // the removal below on a later pass, and it takes a REMOVED operation's view out of
        // "whatever parent it has" — which by then may be the host of a page just re-mounted by
        // a navigation that landed mid-transition.
        if (leaving.Page.Handler?.PlatformView is AView pageView && pageView.Parent is AViewGroup pageHost)
        {
            pageHost.RemoveView(pageView);
        }

        if (leaving.View.Parent is AViewGroup parent)
        {
            parent.RemoveView(leaving.View);
        }

        leaving.FragmentManager
               .BeginTransaction()
               .SetReorderingAllowed(true)
               .Remove(leaving.Fragment)
               .CommitAllowingStateLoss();
    }

    private void RemovePeek(AView? peekView)
    {
        // The commit sync re-parents this exact platform view into the new fragment — never
        // detach it once it is no longer OUR peek (it may already be the presented page).
        if (peekView is not null && ReferenceEquals(peekView.Parent, _pageLayer))
        {
            _pageLayer?.RemoveView(peekView);
        }

        // Either way its per-peek inset intent ends here: an adopted view is padded by the
        // layer-wide intent the sync just set (same values — the peek precomputed them).
        if (peekView is not null)
        {
            _pageLayer?.ClearPeekInsetIntent(peekView);
        }
    }

    private FragmentContainerView EnsureContainer(ScaffoldLayout platformView)
    {
        // The host platform view changes when the activity is recreated (system back at root,
        // configuration change): the old layers and mounted fragment died with it.
        if (_container is not null && ReferenceEquals(_hostPlatformView, platformView))
        {
            return _container;
        }

        _hostPlatformView = platformView;
        _currentFragment = null;
        _currentPage = null;
        _tabBarStrip = null;
        _currentBarView = null;
        _currentTabBarArea = null;
        _barPresented = false;
        _stripAnimator = null;
        _navBarStrip = null;
        _navBarHost?.Dispose();
        _navBarHost = null;
        _navStripAnimator = null;
        _backPreviewActive = false;
        _backPeekView = null;
        _backTopView = null;
        _backBelowPage = null;
        _backFlightSession = null;
        _predictiveHandoffPage = null;
        _leavingPage = null;

        var context = platformView.Context!;

        // Page layer: participates in the insets chain and rewrites the bottom system-bars
        // inset to the chrome footprint (§5.4) before insets reach the hosted page views.
        var pageLayer = new ScaffoldPageLayerLayout(context);
        _pageLayer = pageLayer;
        platformView.PageLayer = pageLayer;
        platformView.AddView(pageLayer, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));

        var container = new FragmentContainerView(context) { Id = AView.GenerateViewId() };
        _container = container;
        pageLayer.AddView(container, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));

        return container;
    }

    /// <summary>
    /// Brings the chrome to the desired state. Visibility changes RETARGET any in-flight
    /// slide from its current position (the previous animator is canceled — no queue, no
    /// teardown): the strip stays mounted while its area is a tab bar — hidden just means
    /// translated offscreen — so rapid toggles reverse smoothly and re-showing is instant.
    /// The bar view's logical attachment still tracks presented state (the element tree
    /// reflects presented chrome).
    /// </summary>
    private Task UpdateTabBarChromeAsync(ScaffoldLayout platformView, IMauiContext mauiContext, ScaffoldTabBar? tabBarArea, bool barVisible, bool animated)
    {
        if (tabBarArea is null)
        {
            // Area without a tab bar: tear the strip down entirely (animated slide-out first).
            if (_currentBarView is null || _tabBarStrip is not { } strip)
            {
                return Task.CompletedTask;
            }

            var previousArea = _currentTabBarArea;
            _currentBarView = null;
            _currentTabBarArea = null;
            _barPresented = false;

            return UnmountAsync(strip, previousArea);
        }

        if (barVisible)
        {
            var barView = tabBarArea.GetOrCreateBarView();

            if (_tabBarStrip is null)
            {
                _tabBarStrip = new ScaffoldTabBarStripLayout(platformView.Context!);
                platformView.TabBarLayer = _tabBarStrip;

                platformView.AddView(
                    _tabBarStrip,
                    new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent)
                    {
                        Gravity = GravityFlags.Bottom
                    }
                );
            }

            var freshMount = false;

            if (!ReferenceEquals(barView, _currentBarView))
            {
                var previousArea = _currentTabBarArea;
                var previousBarView = _currentBarView;
                _currentBarView = barView;
                _currentTabBarArea = tabBarArea;
                _tabBarStrip.SetBar(barView.ToPlatform(mauiContext));
                freshMount = true;

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

            _tabBarStrip.Visibility = ViewStates.Visible;

            if (freshMount && !_barPresented && animated && _lastStripHeight > 0)
            {
                // A freshly appearing bar starts below the edge and slides in with the pop.
                _tabBarStrip.TranslationY = _lastStripHeight;
            }

            _barPresented = true;

            if (_tabBarStrip.Height > 0)
            {
                _lastStripHeight = _tabBarStrip.Height;
            }

            return ShowAsync(_tabBarStrip);

            async Task ShowAsync(ScaffoldTabBarStripLayout strip)
            {
                // The slide-in starts away from rest: keep the insets frozen (see FreezeInsets)
                // for the whole flight so the bar keeps its resting padding while it crosses
                // the system-bars region, then recompute once settled.
                if (strip.TranslationY != 0)
                {
                    strip.FreezeInsets();
                }

                await AnimateStripToAsync(strip, 0, animated);

                if (strip.TranslationY == 0)
                {
                    strip.UnfreezeInsets();
                }
            }
        }

        // Hidden: keep the strip alive offscreen; only the logical attachment reflects it.
        _currentTabBarArea?.OnBarViewUnmounted();
        _barPresented = false;

        if (_tabBarStrip is not { } hiddenStrip)
        {
            return Task.CompletedTask;
        }

        if (hiddenStrip.Height > 0)
        {
            _lastStripHeight = hiddenStrip.Height;
        }

        if (_lastStripHeight <= 0)
        {
            return Task.CompletedTask;
        }

        // Freeze the insets at their resting values BEFORE leaving rest (see FreezeInsets):
        // the bar must not be re-padded while translated through the system-bars region.
        hiddenStrip.FreezeInsets();

        return AnimateStripToAsync(hiddenStrip, _lastStripHeight, animated);

        async Task UnmountAsync(ScaffoldTabBarStripLayout stripToRemove, ScaffoldTabBar? previousArea)
        {
            if (stripToRemove.Height > 0)
            {
                _lastStripHeight = stripToRemove.Height;
                stripToRemove.FreezeInsets();
                await AnimateStripToAsync(stripToRemove, stripToRemove.Height, animated);
            }

            stripToRemove.Visibility = ViewStates.Gone;
            stripToRemove.TranslationY = 0;
            stripToRemove.SetBar(null);
            stripToRemove.UnfreezeInsets(requestApply: false);
            previousArea?.OnBarViewUnmounted();
        }
    }

    /// <summary>
    /// Retargets the strip's slide: the previous animator is canceled and the new one starts
    /// from the CURRENT translation — rapid toggles reverse smoothly mid-flight.
    /// </summary>
    private Task AnimateStripToAsync(AView strip, float target, bool animated)
    {
        _stripAnimator?.Cancel();
        _stripAnimator = AnimateTranslationCore(strip, target, animated, out var task);

        return task;
    }

    private Task AnimateNavStripToAsync(AView strip, float target, bool animated)
    {
        _navStripAnimator?.Cancel();
        _navStripAnimator = AnimateTranslationCore(strip, target, animated, out var task);

        return task;
    }

    private static Android.Animation.ObjectAnimator? AnimateTranslationCore(AView strip, float target, bool animated, out Task task)
    {
        if (!animated)
        {
            strip.TranslationY = target;
            task = Task.CompletedTask;

            return null;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var animator = Android.Animation.ObjectAnimator.OfFloat(strip, "translationY", strip.TranslationY, target)!;
        animator.SetDuration(_overlayDurationMs);
        animator.AnimationEnd += (_, _) => completion.TrySetResult();
        animator.Start();
        task = completion.Task;

        return animator;
    }

    /// <summary>
    /// Brings the nav bar chrome to the desired state — same model as the tab bar strip:
    /// mounted while a bar view resolves (hidden = translated above the screen edge) and
    /// retargeting slides. The strip hosts the library-owned <see cref="ScaffoldNavBarHost"/>
    /// (mounted once): bar-view resolution changes swap the bar VIRTUALLY inside it (instant,
    /// no strip re-mount), and the effective <see cref="ScaffoldNavBarAppearance"/> lands on
    /// the host — never on the bar view.
    /// </summary>
    private Task UpdateNavBarChromeAsync(ScaffoldLayout platformView, IMauiContext mauiContext, Page targetPage, View? navBarView, bool navBarVisible, bool animated)
    {
        EnsureSystemBarApplier(platformView);
        scaffold.SystemBars.NavBarVisible = navBarView is not null && navBarVisible;

        if (navBarView is null)
        {
            if (_navBarHost is { } clearedHost)
            {
                if (clearedHost.Bar is not null)
                {
                    _navBarStrip?.SetBar(null);

                    if (_navBarStrip is not null)
                    {
                        _navBarStrip.Visibility = ViewStates.Gone;
                    }

                    DetachNavBarHost(clearedHost);
                    clearedHost.SetBar(null);
                }

                // The host keeps tracking the CURRENT page even bar-less: the previous page's
                // scroll observation (KVO / listeners) must not outlive its page.
                clearedHost.UpdateSources(targetPage);
            }

            return Task.CompletedTask;
        }

        if (_navBarStrip is null)
        {
            _navBarStrip = new ScaffoldNavBarStripLayout(platformView.Context!);
            platformView.NavBarLayer = _navBarStrip;

            var layoutParams = new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Top
            };

            // Below the tab bar strip in z-order: behind-chrome overlay scrims dim the nav bar
            // while keeping the tab bar interactive.
            if (_tabBarStrip is { } tabBarStrip && platformView.IndexOfChild(tabBarStrip) is >= 0 and var tabIndex)
            {
                platformView.AddView(_navBarStrip, tabIndex, layoutParams);
            }
            else
            {
                platformView.AddView(_navBarStrip, layoutParams);
            }
        }

        var host = _navBarHost ??= new ScaffoldNavBarHost(scaffold);
        var freshMount = host.Bar is null;
        host.SetBar(navBarView);
        host.UpdateSources(targetPage);

        if (freshMount)
        {
            _navBarStrip.SetBar(host.ToPlatform(mauiContext));

            if (animated && _lastNavStripHeight > 0)
            {
                // A freshly appearing strip starts above the edge and slides in.
                _navBarStrip.TranslationY = -_lastNavStripHeight;
            }
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

        if (_navBarStrip.Height > 0)
        {
            _lastNavStripHeight = _navBarStrip.Height;
        }

        if (navBarVisible)
        {
            _navBarStrip.Visibility = ViewStates.Visible;

            return ShowNavAsync(_navBarStrip);
        }

        if (_lastNavStripHeight <= 0)
        {
            // Hidden and never measured — a page that starts bar-less. There is no height to
            // translate the strip by, so it must be taken OUT of the layout: left merely
            // "visible at rest" it bands the top of the page with chrome that does not belong to
            // it (until some later navigation finally measures it and slides it away).
            _navBarStrip.Visibility = ViewStates.Gone;

            return Task.CompletedTask;
        }

        _navBarStrip.Visibility = ViewStates.Visible;

        // Same contract as the tab bar strip: freeze the insets at their resting values
        // before leaving rest so the translated bar is never re-padded mid-flight.
        _navBarStrip.FreezeInsets();

        return AnimateNavStripToAsync(_navBarStrip, -_lastNavStripHeight, animated);

        async Task ShowNavAsync(ScaffoldNavBarStripLayout strip)
        {
            if (strip.TranslationY != 0)
            {
                strip.FreezeInsets();
            }

            await AnimateNavStripToAsync(strip, 0, animated);

            if (strip.TranslationY == 0)
            {
                strip.UnfreezeInsets();
            }
        }
    }

    private void DetachNavBarHost(ScaffoldNavBarHost host)
    {
        if (ReferenceEquals(host.Parent, scaffold))
        {
            scaffold.RemoveLogicalChild(host);
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
            || scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext })
        {
            return;
        }

        var tabBarArea = root.Parent as ScaffoldTabBar;
        var barVisible = tabBarArea is not null && Scaffold.ComputeTabBarVisible(root, page);
        UpdateTabBarChromeAsync(platformView, mauiContext, tabBarArea, barVisible, animated: false).FireAndForget(scaffold.Handler);
    }

    /// <summary>Re-resolves and re-presents the nav bar chrome for the current page.</summary>
    private void RefreshNavBarChrome()
    {
        if (_currentPage is not { } page
            || scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext })
        {
            return;
        }

        var navBarView = scaffold.ResolveNavBarView(page);
        var navBarVisible = navBarView is not null && Scaffold.GetIsNavBarVisible(page);
        var navBarInsets = navBarVisible && !Scaffold.GetNavBarOverlapsContent(page);

        // Same-page toggle: the page itself must relayout to the new insets.
        platformView.ChromeTopDesired = navBarInsets;
        platformView.PageTopInsetPx = navBarInsets ? _lastNavStripHeight : 0;
        RequestPageInsets();

        UpdateNavBarChromeAsync(platformView, mauiContext, page, navBarView, navBarVisible, animated: true).FireAndForget(scaffold.Handler);
    }

    /// <summary>Hides the soft keyboard when an input on the outgoing page holds focus.</summary>
    private static void HideSoftInputBeforeNavigation(Page previousPage)
    {
        if (previousPage.Handler?.PlatformView is not AView previousPlatformView
            || previousPlatformView.FindFocus() is not { } focusedView)
        {
            return;
        }

        if (previousPlatformView.Context?.GetSystemService(Android.Content.Context.InputMethodService) is Android.Views.InputMethods.InputMethodManager inputMethodManager)
        {
            inputMethodManager.HideSoftInputFromWindow(previousPlatformView.WindowToken, Android.Views.InputMethods.HideSoftInputFlags.None);
        }

        focusedView.ClearFocus();
    }

    /// <summary>
    /// Routes the resolved system-bar icon style to the window (SystemUI fades the change) and
    /// installs the PixelCopy sampler — the ground-truth read of the app pixels rendered under
    /// the status bar (the status bar itself is a SystemUI window, never part of the copy).
    /// Covers theme toggles too: MAUI raises RequestedThemeChanged on uiMode configuration
    /// changes (the activity is NOT recreated — ConfigChanges.UiMode — so the theme's
    /// windowLightStatusBar attribute, resolved only at creation, would otherwise go stale).
    /// </summary>
    private void EnsureSystemBarApplier(ScaffoldLayout platformView)
    {
        if (_systemBarApplierAttached)
        {
            return;
        }

        _systemBarApplierAttached = true;

        scaffold.SystemBars.SetApplier(lightIcons =>
        {
            if (WindowOf(platformView) is { } window)
            {
                // AppearanceLight* = true means DARK icons over a light bar.
                var controller = new WindowInsetsControllerCompat(window, platformView);
                controller.AppearanceLightStatusBars = !lightIcons;
                controller.AppearanceLightNavigationBars = !lightIcons;
            }
        });

        scaffold.SystemBars.SetSampler(() => OperatingSystem.IsAndroidVersionAtLeast(26)
            ? SampleStatusBarStripAsync(platformView)
            : Task.FromResult<double?>(null));

        scaffold.SystemBars.SetThemeRefresher(() => RefreshNavigationBarColor(platformView));
    }

    private bool _systemBarApplierAttached;

    /// <summary>
    /// Re-resolves the theme's <c>android:navigationBarColor</c> and re-applies it: the window
    /// only reads the attribute at activity creation, so on a theme toggle without recreation
    /// (ConfigChanges.UiMode) the bottom system bar keeps the previous theme's color — most
    /// visible with 3-button navigation, which honors an opaque color even on Android 15+
    /// (only gesture navigation ignores it under enforced edge-to-edge). The activity
    /// resources are night-aware without a recreation, so resolving the attribute NOW yields
    /// the new theme's value.
    /// </summary>
    private static void RefreshNavigationBarColor(ScaffoldLayout platformView)
    {
        if (WindowOf(platformView) is not { } window
            || window.Context?.Theme is not { } theme)
        {
            return;
        }

        var value = new Android.Util.TypedValue();

#pragma warning disable CA1422 // Obsolete FROM 35, but 3-button navigation still honors an opaque color there.
        if (theme.ResolveAttribute(Android.Resource.Attribute.NavigationBarColor, value, resolveRefs: true)
            && value.Type is >= Android.Util.DataType.FirstColorInt and <= Android.Util.DataType.LastColorInt)
        {
            window.SetNavigationBarColor(new Android.Graphics.Color(value.Data));
        }
#pragma warning restore CA1422
    }

    private static Android.Views.Window? WindowOf(AView view)
    {
        var context = view.Context;

        while (context is Android.Content.ContextWrapper wrapper and not Android.App.Activity)
        {
            context = wrapper.BaseContext;
        }

        return context is Android.App.Activity { Window: { } window } ? window : null;
    }

    /// <summary>
    /// Average luminance [0, 1] of the app content under the status bar: PixelCopy of the strip
    /// downsampled into a tiny bitmap (the copy scales, so the cost is microseconds) — null when
    /// the window is not ready or the copy fails.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
    private static Task<double?> SampleStatusBarStripAsync(ScaffoldLayout platformView)
    {
        if (WindowOf(platformView) is not { } window
            || window.DecorView is not { Width: > 0, IsAttachedToWindow: true } decor)
        {
            return Task.FromResult<double?>(null);
        }

        var inset = ViewCompat.GetRootWindowInsets(platformView)?.GetInsets(WindowInsetsCompat.Type.StatusBars()) is { } insets ? insets.Top : 0;

        if (inset < 1)
        {
            return Task.FromResult<double?>(null);
        }

        const int sampleWidth = 32;
        const int sampleHeight = 4;
        var bitmap = Android.Graphics.Bitmap.CreateBitmap(sampleWidth, sampleHeight, Android.Graphics.Bitmap.Config.Argb8888!);
        var completion = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);

        PixelCopy.Request(
            window,
            new Android.Graphics.Rect(0, 0, decor.Width, inset),
            bitmap,
            new PixelCopyListener(status =>
            {
                if (status == (int)PixelCopyResult.Success)
                {
                    var pixels = new int[sampleWidth * sampleHeight];
                    bitmap.GetPixels(pixels, 0, sampleWidth, 0, 0, sampleWidth, sampleHeight);
                    double total = 0;

                    foreach (var pixel in pixels)
                    {
                        var r = (pixel >> 16) & 0xFF;
                        var g = (pixel >> 8) & 0xFF;
                        var b = pixel & 0xFF;
                        total += ((0.2126 * r) + (0.7152 * g) + (0.0722 * b)) / 255.0;
                    }

                    completion.TrySetResult(total / pixels.Length);
                }
                else
                {
                    completion.TrySetResult(null);
                }

                bitmap.Recycle();
            }),
            new Android.OS.Handler(Android.OS.Looper.MainLooper!));

        return completion.Task;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
    private sealed class PixelCopyListener(Action<int> onFinished) : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
    {
        public void OnPixelCopyFinished(int copyResult) => onFinished(copyResult);
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
            scaffold.SystemBars.SetThemeRefresher(null);
            scaffold.SystemBars.SetApplier(null);
        }

        ObserveNavBarArea(null);
        FinishLeavingPage();
        _navBarHost?.Dispose();
        _navBarHost = null;
    }

    private void OnCurrentPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Page page
            || !ReferenceEquals(page, _currentPage)
            || scaffold.Proxy?.CurrentItem.CurrentSection is not ScaffoldRootProxy rootProxy
            || scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext })
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

                // Same-page toggle: the page itself must relayout to the new insets.
                platformView.ChromeBottomDesired = barVisible;
                platformView.PageBottomInsetPx = barVisible ? _lastStripHeight : 0;
                RequestPageInsets();

                UpdateTabBarChromeAsync(platformView, mauiContext, tabBarArea, barVisible, animated: true).FireAndForget(scaffold.Handler);

                break;
            }

            case "IsNavBarVisible":
            case "NavBarView":
            case "NavBarOverlapsContent":
                RefreshNavBarChrome();

                break;
        }
    }

    private void RequestPageInsets()
    {
        if (_pageLayer is { } pageLayer)
        {
            ViewCompat.RequestApplyInsets(pageLayer);
        }
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
        if (scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext }
            || platformView.Context is not { } context)
        {
            request.Cleanup?.Invoke();

            return false;
        }

        // The tab bar panel slot sits BELOW the bottom chrome strip in z-order — the bar
        // renders above the scrim, undimmed and interactive. Everything else stacks on top.
        var chromeLayer = request.Kind == ScaffoldOverlayKind.TabBarPanel && _tabBarStrip is { Visibility: ViewStates.Visible } strip ? strip : null;
        var chromeLayerIndex = chromeLayer is null ? -1 : platformView.IndexOfChild(chromeLayer);

        var scrimView = request.CreateScrimView();

        // The element tree reflects presented chrome: the scrim participates while mounted
        // (tooling and UI tests can see and tap it).
        scaffold.AddLogicalChild(scrimView);
        AttachScrimTap(scrimView, request);
        var scrim = scrimView.ToPlatform(mauiContext);
        scrim.Clickable = true;

        var scrimLayoutParams = new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent);

        if (chromeLayerIndex >= 0)
        {
            platformView.AddView(scrim, chromeLayerIndex++, scrimLayoutParams);
        }
        else
        {
            platformView.AddView(scrim, scrimLayoutParams);
        }

        double flyoutOffscreen = 0;
        AView panel;

        switch (request.Kind)
        {
            case ScaffoldOverlayKind.Flyout:
            {
                panel = request.Content.ToPlatform(mauiContext);
                (panel.Parent as AViewGroup)?.RemoveView(panel);

                // Entrance offset rides the MAUI translation (dp), applied after mounting.
                flyoutOffscreen = LayoutFlyout(request, panel, platformView, context);
                platformView.AddView(panel);

                // The flyout covers the status-bar region: its surface drives the icon style
                // while open (SystemUI fades the flip alongside the slide).
                scaffold.SystemBars.OverlaySurface = ScaffoldSystemBars.SurfaceColorOf(request.Content);

                break;
            }

            case ScaffoldOverlayKind.Popup:
            {
                panel = request.Content.ToPlatform(mauiContext);
                (panel.Parent as AViewGroup)?.RemoveView(panel);

                // The host is the popup's IME isolation boundary (the presenter keeps the popup
                // above the keyboard) — see ScaffoldPopupHost.
                var popupHost = new ScaffoldPopupHost(context);
                popupHost.AddView(panel, new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
                panel = popupHost;

                platformView.AddView(panel, LayoutPopup(request, platformView, context, platformView.ImeBottomInsetPx));

                break;
            }

            case ScaffoldOverlayKind.BottomSheet:
            {
                var sheet = (ScaffoldBottomSheetView)request.Content;
                panel = request.Content.ToPlatform(mauiContext);
                (panel.Parent as AViewGroup)?.RemoveView(panel);

                // The nested host between the container and the sheet provides the drag/scroll
                // cooperative hand-off (expand-then-scroll, pull-down at scroll top) — see
                // ScaffoldBottomSheetNestedHost.
                var nestedHost = new ScaffoldBottomSheetNestedHost(context, sheet);
                nestedHost.AddView(panel, new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
                panel = nestedHost;

                platformView.AddView(panel, LayoutBottomSheet(sheet, request.KeyboardMode, panel, platformView, context, initial: true, platformView.ImeBottomInsetPx));

                break;
            }

            default:
            {
                panel = MountTabBarPanelContent(request.Content, platformView, context, mauiContext, chromeLayerIndex);

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
        scaffold.UpdateBackCallbackEnabled();

        // A new sheet/popup takes the keyboard over from the page (or the entry below it), and
        // follows its content's natural size from now on.
        if (request.Kind is ScaffoldOverlayKind.Popup or ScaffoldOverlayKind.BottomSheet)
        {
            OnKeyboardOwnerChanged(platformView, context);
            ObserveContentMeasure(entry, platformView, context);
        }

        // MAUI's net10 safe-area pass evaluates each layout at its FIRST traversal with an
        // off-screen heuristic: a view laid out while translated off the screen receives FULL
        // system-bar padding ("it will settle at origin"), permanently displacing overlay
        // content. Let the subtree settle a traversal at its REAL position — hidden — before
        // the entrance translation applies.
        panel.Alpha = 0;
        await Task.Delay(32);
        panel.Alpha = 1;

        ScaffoldOverlayAnimations.PrepareEnter(request, flyoutOffscreen);
        await ScaffoldOverlayAnimations.EnterAsync(request, scrimView);

        return true;
    }

    /// <summary>
    /// Sizes and pins a flyout against the CURRENT window — its configured width (typically a
    /// fraction of the window's), full height, on its edge — and returns the offscreen
    /// translation that side implies. Leaves TranslationX alone, so an open flyout stays open.
    /// </summary>
    private double LayoutFlyout(ScaffoldOverlayRequest request, AView panel, AView container, Android.Content.Context context)
    {
        var options = scaffold.GetEffectiveFlyoutOptions(request.FlyoutSide);
        var widthDp = options.ComputeWidth(context.FromPixels(container.Width));
        var onLeft = IsFlyoutOnLeft(request.FlyoutSide);

        panel.LayoutParameters = new Android.Widget.FrameLayout.LayoutParams((int)context.ToPixels(widthDp), AViewGroup.LayoutParams.MatchParent)
        {
            Gravity = onLeft ? GravityFlags.Left : GravityFlags.Right
        };

        return onLeft ? -widthDp : widthDp;
    }

    /// <summary>
    /// Resolves a popup's placement against the CURRENT window: the available area is the window
    /// minus its system insets and the popup's margin, and an anchored popup follows wherever its
    /// anchor now sits. Returns the layout params to mount (or re-mount) it with.
    /// </summary>
    private Android.Widget.FrameLayout.LayoutParams LayoutPopup(ScaffoldOverlayRequest request, ScaffoldLayout container, Android.Content.Context context, int keyboardOverlapPx)
    {
        var systemInsets = ViewCompat.GetRootWindowInsets(container)?.GetInsets(WindowInsetsCompat.Type.SystemBars());
        var presentation = request.PopupPresentation!;
        var margin = presentation.Margin;

        // Resize: the keyboard is a bottom inset for placement purposes (an anchored popup that no
        // longer fits below flips above, a centered one centers in what is left). Pan / None: the
        // popup is placed as if there were no keyboard (Pan then slides it — below).
        var keyboardOverlap = context.FromPixels(keyboardOverlapPx);
        var bottomInsetPx = request.KeyboardMode == ScaffoldKeyboardMode.Resize
            ? ScaffoldOverlayGeometry.BottomInset(systemInsets?.Bottom ?? 0, keyboardOverlapPx)
            : systemInsets?.Bottom ?? 0;

        var area = new Rect(
            context.FromPixels(systemInsets?.Left ?? 0) + margin.Left,
            context.FromPixels(systemInsets?.Top ?? 0) + margin.Top,
            Math.Max(0, context.FromPixels(container.Width - (systemInsets?.Left ?? 0) - (systemInsets?.Right ?? 0)) - margin.HorizontalThickness),
            Math.Max(0, context.FromPixels(container.Height - (systemInsets?.Top ?? 0) - bottomInsetPx) - margin.VerticalThickness)
        );

        // VIRTUAL measure, not a native one: measuring the platform view directly bypasses the
        // cross-platform pass, so the content wraps its native children and Width/HeightRequest
        // are silently ignored — a 240dp popup came out label-sized. Same contract as the flyout.
        var popupView = (IView)request.Content;
        var desired = popupView.Measure(area.Width, area.Height);

        var contentSize = new Size(
            Math.Min(desired.Width, area.Width),
            Math.Min(desired.Height, area.Height)
        );

        Rect? anchorBounds = null;

        if (presentation.Anchor is { Handler.PlatformView: AView anchorView })
        {
            var anchorLocation = new int[2];
            var containerLocation = new int[2];
            anchorView.GetLocationInWindow(anchorLocation);
            container.GetLocationInWindow(containerLocation);

            anchorBounds = new Rect(
                context.FromPixels(anchorLocation[0] - containerLocation[0]),
                context.FromPixels(anchorLocation[1] - containerLocation[1]),
                context.FromPixels(anchorView.Width),
                context.FromPixels(anchorView.Height)
            );
        }

        var rect = ScaffoldPopupPlacementResolver.Resolve(presentation, area, contentSize, anchorBounds, scaffold.IsRightToLeft);

        if (request.KeyboardMode == ScaffoldKeyboardMode.Pan && keyboardOverlap > 0)
        {
            var focusedBottom = request.Content.Handler?.PlatformView is AView panel ? ScaffoldFocusedInput.BottomIn(panel, context) : null;

            rect.Y -= ScaffoldOverlayGeometry.Pan(
                context.FromPixels(container.Height) - keyboardOverlap,
                rect.Top,
                rect.Bottom,
                focusedBottom is { } inPanel ? rect.Top + inPanel : null,
                context.FromPixels(systemInsets?.Top ?? 0) + margin.Top
            );
        }

        // Virtual arrange gives the content a valid MAUI frame at its resolved size (the platform
        // margins below position the slot); without it the transform mappers have no frame to
        // apply against and the virtual bounds stay invalid.
        popupView.Arrange(new Rect(0, 0, rect.Width, rect.Height));

        return new Android.Widget.FrameLayout.LayoutParams((int)context.ToPixels(rect.Width), (int)context.ToPixels(rect.Height))
        {
            Gravity = GravityFlags.Left | GravityFlags.Top,
            LeftMargin = (int)context.ToPixels(rect.X),
            TopMargin = (int)context.ToPixels(rect.Y)
        };
    }

    /// <summary>
    /// Frames a bottom sheet against the CURRENT window: capped width, centered, bottom-anchored,
    /// with its detents resolved against the height available above the top inset. Returns the
    /// layout params to mount (or re-mount) it with.
    /// </summary>
    /// <param name="sheet">The presented sheet.</param>
    /// <param name="keyboardMode">How the sheet reacts to the soft keyboard.</param>
    /// <param name="keyboardOverlapPx">The keyboard overlap (px) this sheet reacts to (0 unless it OWNS the keyboard — see <see cref="KeyboardOwner"/>).</param>
    /// <param name="panel">The sheet's platform view (the nested host).</param>
    /// <param name="container">The overlay container the sheet is framed against.</param>
    /// <param name="context">The Android context, for pixel conversions.</param>
    /// <param name="initial">
    /// True on presentation (geometry is being established); false on a re-layout, where the sheet
    /// keeps the detent it rests on while its heights are re-derived for the new window.
    /// </param>
    private static Android.Widget.FrameLayout.LayoutParams LayoutBottomSheet(
        ScaffoldBottomSheetView sheet,
        ScaffoldKeyboardMode keyboardMode,
        AView panel,
        ScaffoldLayout container,
        Android.Content.Context context,
        bool initial,
        int keyboardOverlapPx)
    {
        var insets = ViewCompat.GetRootWindowInsets(container)?.GetInsets(WindowInsetsCompat.Type.SystemBars());
        var keyboardOverlap = context.FromPixels(keyboardOverlapPx);

        var availableHeight = context.FromPixels(container.Height - (insets?.Top ?? 0));

        // Resize: a visible keyboard is a bigger bottom inset — the sheet surface stays anchored
        // to the bottom edge while its content is padded up to the keyboard's top edge (see
        // ScaffoldOverlayGeometry). Pan / None: system inset only.
        var bottomPaddingPx = keyboardMode == ScaffoldKeyboardMode.Resize
            ? ScaffoldOverlayGeometry.SheetBottomPadding(insets?.Bottom ?? 0, keyboardOverlapPx)
            : insets?.Bottom ?? 0;

        // Padding first (it affects the natural height), then measure, then geometry.
        sheet.PrepareForMeasure(context.FromPixels(bottomPaddingPx));

        var sheetWidthPx = (int)Math.Min(container.Width, context.ToPixels(sheet.MaxWidth));

        panel.Measure(
            AView.MeasureSpec.MakeMeasureSpec(sheetWidthPx, MeasureSpecMode.Exactly),
            AView.MeasureSpec.MakeMeasureSpec((int)context.ToPixels(availableHeight), MeasureSpecMode.AtMost)
        );

        var natural = Math.Min(context.FromPixels(panel.MeasuredHeight), availableHeight);

        var sheetHeight = initial
            ? sheet.InitializeGeometry(availableHeight, natural)
            : sheet.UpdateGeometry(availableHeight, natural);

        // Pan: the whole sheet slides up by the least that keeps the focused input above the
        // keyboard (its resting detent translation still applies on top of the frame).
        double pan = 0;

        if (keyboardMode == ScaffoldKeyboardMode.Pan && keyboardOverlap > 0)
        {
            var containerHeight = context.FromPixels(container.Height);
            var visibleTop = containerHeight - sheetHeight + sheet.TranslationY;
            var focusedBottom = ScaffoldFocusedInput.BottomIn(panel, context);

            pan = ScaffoldOverlayGeometry.Pan(
                containerHeight - keyboardOverlap,
                visibleTop,
                containerHeight,
                focusedBottom is { } inPanel ? containerHeight - sheetHeight + inPanel : null,
                context.FromPixels(insets?.Top ?? 0)
            );
        }

        return new Android.Widget.FrameLayout.LayoutParams(sheetWidthPx, (int)context.ToPixels(sheetHeight))
        {
            Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal,
            BottomMargin = (int)context.ToPixels(pan)
        };
    }

    /// <summary>
    /// Re-places the overlays whose geometry depends on the soft keyboard (sheets, popups) after
    /// its overlap changed — per animation frame while it moves.
    /// </summary>
    /// <summary>
    /// The presented entry that OWNS the soft keyboard: the topmost sheet or popup — the keyboard
    /// inset is applied to that surface alone; when none is presented, the page owns it (see
    /// <see cref="ScaffoldLayout.OverlayOwnsKeyboard"/>).
    /// </summary>
    private OverlayEntry? KeyboardOwner
        => _overlays.LastOrDefault(static entry => !entry.Closing && entry.Request.Kind is ScaffoldOverlayKind.Popup or ScaffoldOverlayKind.BottomSheet);

    private int KeyboardOverlapFor(OverlayEntry entry, ScaffoldLayout container)
        => ReferenceEquals(entry, KeyboardOwner) ? container.ImeBottomInsetPx : 0;

    /// <summary>The keyboard's owner changed (an entry presented or closed): overlays and page re-apply their keyboard reaction.</summary>
    private void OnKeyboardOwnerChanged(ScaffoldLayout platformView, Android.Content.Context context)
    {
        if (platformView.ImeBottomInsetPx > 0)
        {
            RelayoutKeyboardAwareOverlays(platformView, context);
        }

        (platformView.PageLayer as ScaffoldPageLayerLayout)?.ApplyKeyboard();
    }

    private void RelayoutKeyboardAwareOverlays(ScaffoldLayout container, Android.Content.Context context)
    {
        foreach (var entry in _overlays.ToArray())
        {
            if (entry.Closing || entry.ContentPlatform is not { } panel)
            {
                continue;
            }

            switch (entry.Request)
            {
                case { Content: ScaffoldBottomSheetView sheet }:
                    panel.LayoutParameters = LayoutBottomSheet(sheet, entry.Request.KeyboardMode, panel, container, context, initial: false, KeyboardOverlapFor(entry, container));

                    break;

                case { Kind: ScaffoldOverlayKind.Popup }:
                    panel.LayoutParameters = LayoutPopup(entry.Request, container, context, KeyboardOverlapFor(entry, container));

                    break;
            }
        }
    }

    /// <summary>
    /// Re-lays out presented overlays after the window changed shape (rotation, split view).
    /// </summary>
    /// <remarks>
    /// Overlay geometry is computed at presentation from the window of that moment: a rotation
    /// otherwise leaves a bottom sheet at its portrait width and portrait detent heights — off the
    /// side of the screen and taller than the window it now sits in — while its scrim still dims
    /// everything. Only the sheet is re-laid out here; anchored popups and panels have the same
    /// exposure and are not covered yet.
    /// </remarks>
    private void RelayoutOverlays(ScaffoldLayout container, Android.Content.Context context)
    {
        var closePanel = false;

        // Snapshot: closing the panel mutates the list.
        foreach (var entry in _overlays.ToArray())
        {
            if (entry.Closing || entry.ContentPlatform is not { } panel)
            {
                continue;
            }

            switch (entry.Request)
            {
                case { Content: ScaffoldBottomSheetView sheet }:
                    panel.LayoutParameters = LayoutBottomSheet(sheet, entry.Request.KeyboardMode, panel, container, context, initial: false, KeyboardOverlapFor(entry, container));

                    break;

                case { Kind: ScaffoldOverlayKind.Popup }:
                    panel.LayoutParameters = LayoutPopup(entry.Request, container, context, KeyboardOverlapFor(entry, container));

                    break;

                case { Kind: ScaffoldOverlayKind.Flyout }:
                    LayoutFlyout(entry.Request, panel, container, context);

                    break;

                // Dismissed by a shape change rather than re-laid out — same contract as iOS; see
                // the note there.
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
    /// Mounts a tab bar panel at its resting position: hugging its content, centered, above the
    /// bottom chrome footprint (inserted below the strip when present).
    /// </summary>
    private AView MountTabBarPanelContent(View content, ScaffoldLayout platformView, Android.Content.Context context, IMauiContext mauiContext, int chromeLayerIndex)
    {
        var gapPx = (int)context.ToPixels(_overflowGapDp);
        var excludedBottom = chromeLayerIndex >= 0 ? platformView.ChromeBottomFootprint : 0;
        var margin = content.Margin;

        var panel = content.ToPlatform(mauiContext);
        (panel.Parent as AViewGroup)?.RemoveView(panel);

        // The panel hugs its content and centers, mirroring the bar pill's own sizing.
        var panelLayoutParams = new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.WrapContent, AViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal,
            LeftMargin = (int)context.ToPixels(margin.Left),
            RightMargin = (int)context.ToPixels(margin.Right),
            BottomMargin = excludedBottom + gapPx
        };

        if (chromeLayerIndex >= 0)
        {
            platformView.AddView(panel, chromeLayerIndex, panelLayoutParams);
        }
        else
        {
            platformView.AddView(panel, panelLayoutParams);
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

        if (scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext }
            || platformView.Context is not { } context)
        {
            replacement.Cleanup?.Invoke();

            return;
        }

        var previous = entry.Request;
        entry.Request = replacement;

        // Scrim: brush update only, no re-animation.
        entry.ScrimView.Background = replacement.Scrim;

        await previous.Content.FadeToAsync(0, 100);
        (entry.ContentPlatform.Parent as AViewGroup)?.RemoveView(entry.ContentPlatform);
        ScaffoldOverlayAnimations.ResetContent(previous.Content);
        previous.Cleanup?.Invoke();

        if (previous.DisconnectContentOnClose)
        {
            previous.Content.DisconnectHandlers();
        }

        var chromeLayer = _tabBarStrip is { Visibility: ViewStates.Visible } strip ? strip : null;
        var chromeLayerIndex = chromeLayer is null ? -1 : platformView.IndexOfChild(chromeLayer);
        var panel = MountTabBarPanelContent(replacement.Content, platformView, context, mauiContext, chromeLayerIndex);
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

        if (entry.ContentMeasureInvalidated is { } measureHandler)
        {
            request.Content.MeasureInvalidated -= measureHandler;
            entry.ContentMeasureInvalidated = null;
        }

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

        (entry.ContentPlatform.Parent as AViewGroup)?.RemoveView(entry.ContentPlatform);
        (entry.ScrimPlatform.Parent as AViewGroup)?.RemoveView(entry.ScrimPlatform);
        _overlays.Remove(entry);

        // The keyboard goes back to the entry below, or to the page.
        if (request.Kind is ScaffoldOverlayKind.Popup or ScaffoldOverlayKind.BottomSheet
            && scaffold.Handler is IPlatformViewHandler { PlatformView: ScaffoldLayout ownerLayout } && ownerLayout.Context is { } ownerContext)
        {
            OnKeyboardOwnerChanged(ownerLayout, ownerContext);
        }

        ScaffoldOverlayAnimations.ResetContent(request.Content);
        entry.ScrimView.DisconnectHandlers();
        scaffold.RemoveLogicalChild(entry.ScrimView);

        if (request.DisconnectContentOnClose)
        {
            request.Content.DisconnectHandlers();
        }

        entry.ClosedTcs.TrySetResult();
        scaffold.UpdateBackCallbackEnabled();
    }
}
