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
internal sealed class ScaffoldPresenter(Scaffold scaffold) : IScaffoldPresenter
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
    private View? _currentNavBarView;
    private int _lastNavStripHeight;
    private bool _navBarPresented;
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
    }

    private readonly List<OverlayEntry> _overlays = [];

    private ScaffoldRoot? _currentRoot;
    private AView? _backPeekView;
    private AView? _backTopView;
    private Page? _backBelowPage;
    private bool _backPreviewActive;
    private Page? _predictiveHandoffPage;

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
        var animated = hint != ScaffoldPresentationHint.None;

        // The context must carry the target page's state before the bar (or its bindings) mount.
        scaffold.NavBarContext.Update(root, targetPage);

        // Inset intent BEFORE the fragment commit: the incoming page attaches with its final
        // insets while the outgoing page keeps its stale layout — no jumps during transitions.
        platformView.ChromeBottomDesired = barVisible;
        platformView.PageBottomInsetPx = barVisible ? _lastStripHeight : 0;
        platformView.ChromeTopDesired = navBarVisible;
        platformView.PageTopInsetPx = navBarVisible ? _lastNavStripHeight : 0;

        // Chrome and page animate CONCURRENTLY: an Auto-hiding bar slides away while the pushed
        // page slides in (and back in sync on pop).
        // Nav bar first: its strip must sit BELOW the tab bar strip in z-order (behind-chrome
        // overlay scrims dim the nav bar while keeping the tab bar interactive).
        var navChromeTask = UpdateNavBarChromeAsync(platformView, mauiContext, navBarView, navBarVisible, animated);
        var chromeTask = UpdateTabBarChromeAsync(platformView, mauiContext, tabBarArea, barVisible, animated);

        if (!ReferenceEquals(targetPage, _currentPage))
        {
            var previousPage = _currentPage;

            if (previousPage is not null)
            {
                previousPage.PropertyChanged -= OnCurrentPagePropertyChanged;
            }

            // A committed predictive-back preview already settled the visuals (top page
            // offscreen, below page fully revealed via the peek): adopt without re-animating —
            // no shared-element transition, no exit animator on the removed fragment.
            var handoffPage = _predictiveHandoffPage;
            _predictiveHandoffPage = null;
            var predictivelySettled = handoffPage is not null
                && hint == ScaffoldPresentationHint.Pop
                && ReferenceEquals(handoffPage, targetPage);

            // Shared elements (§8, PoC spike B): matching Scaffold.TransitionName pairs between
            // the two pages ride the native androidx transition framework. Both push AND pop are
            // Replace-based here (no fragment back stack), so both directions wire the pairs as
            // an ENTER transition on the incoming fragment.
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
                    _ => ScaffoldPageTransition.Default
                };

            var previousFragment = _currentFragment;
            var fragment = new ScaffoldPageFragment(mauiContext, targetPage, hint, container, pageTransition, postponeForSharedElements: sharedNames.Count > 0);
            _currentFragment = fragment;
            _currentPage = targetPage;
            targetPage.PropertyChanged += OnCurrentPagePropertyChanged;

            // Async commit only: a synchronous commit can run while MAUI's own ScopedFragment
            // transaction is still executing on the same FragmentManager ("already executing").
            var transaction = activity.SupportFragmentManager
                                      .BeginTransaction()
                                      .SetReorderingAllowed(true);

            if (sharedNames.Count > 0)
            {
                var outgoingTagged = ScaffoldTransitions.Collect(previousPage!);
                fragment.SharedElementEnterTransition = CreateSharedElementTransition();

                // The fragment framework IGNORES animators on transition-involved fragments, so
                // the incoming page's slide must ride the transition framework too (the shared
                // element pairs are excluded from it and follow the SET instead).
                var slideEdge = hint switch
                {
                    ScaffoldPresentationHint.Push or ScaffoldPresentationHint.SlideEnd => (int)GravityFlags.End,
                    ScaffoldPresentationHint.SlideStart => (int)GravityFlags.Start,
                    _ => 0
                };

                if (slideEdge != 0)
                {
                    var slide = new AndroidX.Transitions.Slide(slideEdge);
                    slide.SetDuration(_overlayDurationMs);
                    fragment.EnterTransition = slide;
                }

                var sharedSourceViews = new List<View>(sharedNames.Count);

                foreach (var name in sharedNames)
                {
                    var sharedView = outgoingTagged[name];
                    var sharedPlatformView = sharedView.ToPlatform(mauiContext);
                    ViewCompat.SetTransitionName(sharedPlatformView, name);
                    transaction.AddSharedElement(sharedPlatformView, name);
                    sharedSourceViews.Add(sharedView);
                }

                // The SET hides these sources via setTransitionAlpha(0) and only the return SET
                // restores them — record them so paths that skip it (predictive back) can repair.
                ScaffoldPageRestore.CaptureSharedElementSources(previousPage!, sharedSourceViews);
            }

            // The CURRENT navigation decides how the outgoing page leaves (a pop replays the
            // spec's Enter motion reversed; a push plays the incoming spec's Behind motion). A
            // settled predictive-back preview already moved it offscreen — no exit animation.
            previousFragment?.PrepareRemoval(predictivelySettled ? ScaffoldPresentationHint.None : hint, pageTransition);

            transaction
                .Replace(container.Id, fragment)
                .CommitAllowingStateLoss();


            // Deterministic completion: presentation of the new page plus dismissal animation of
            // the previous one, with a settle timeout as a safety net.
            var settled = Task.WhenAll(fragment.PresentedTask, previousFragment?.DismissedTask ?? Task.CompletedTask);
            await Task.WhenAny(settled, Task.Delay(_settleTimeoutMs)).ConfigureAwait(true);
        }

        await Task.WhenAll(navChromeTask, chromeTask).ConfigureAwait(true);
        scaffold.UpdateBackCallbackEnabled();
    }

    private const float _backPreviewMaxShift = 0.4f;

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
            || stack.PushedPages[^1].Page is not { BindingContext: not ILeavingGuard } topPage
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

        // If the peeked page's shared elements took off in the push SET, they are still hidden
        // via transitionAlpha — repair before the page becomes visible under the scrubbed one.
        ScaffoldPageRestore.Repair(belowPage);

        _ = container;
        _backPeekView = belowView;
        _backTopView = topView;
        _backBelowPage = belowPage;
        _backPreviewActive = true;
    }

    /// <summary>Predictive back, gesture progressing: page-motion-only scrub (v1).</summary>
    public void UpdateBackPreview(float progress)
    {
        if (_backPreviewActive && _backTopView is { } topView)
        {
            topView.TranslationX = progress * _backPreviewMaxShift * (topView.Width > 0 ? topView.Width : 0);
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
        _backTopView = null;
        _backPeekView = null;
        _backBelowPage = null;

        if (topView is null)
        {
            RemovePeek(peekView);

            return;
        }

        var animator = Android.Animation.ObjectAnimator.OfFloat(topView, "translationX", topView.TranslationX, 0f)!;
        animator.SetDuration(150);
        animator.AnimationEnd += (_, _) => RemovePeek(peekView);
        animator.Start();
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
        _backTopView = null;
        _backPeekView = null;
        _backBelowPage = null;

        if (topView is null || belowPage is null)
        {
            RemovePeek(peekView);

            return;
        }

        var width = topView.Width > 0 ? topView.Width : 1;
        var settle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animator = Android.Animation.ObjectAnimator.OfFloat(topView, "translationX", topView.TranslationX, width)!;
        animator.SetDuration((long)(_overlayDurationMs * (1 - (topView.TranslationX / width))));
        animator.AnimationEnd += (_, _) => settle.TrySetResult();
        animator.Start();
        await settle.Task.ConfigureAwait(true);

        _predictiveHandoffPage = belowPage;

        var popped = scaffold.NavigationService is { } navigationService
            && await navigationService.GoToAsync(Nalu.Navigation.Relative().Pop()).ConfigureAwait(true);

        if (!popped && ReferenceEquals(_predictiveHandoffPage, belowPage))
        {
            // Engine refused: restore the pre-gesture presentation.
            _predictiveHandoffPage = null;
            var restore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var restoreAnimator = Android.Animation.ObjectAnimator.OfFloat(topView, "translationX", topView.TranslationX, 0f)!;
            restoreAnimator.SetDuration(_overlayDurationMs);
            restoreAnimator.AnimationEnd += (_, _) => restore.TrySetResult();
            restoreAnimator.Start();
            await restore.Task.ConfigureAwait(true);
            RemovePeek(peekView);
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

        RemovePeek(_backPeekView);
        _backTopView = null;
        _backPeekView = null;
        _backBelowPage = null;
    }

    private void RemovePeek(AView? peekView)
    {
        // The commit sync re-parents this exact platform view into the new fragment — never
        // detach it once it is no longer OUR peek (it may already be the presented page).
        if (peekView is not null && ReferenceEquals(peekView.Parent, _pageLayer))
        {
            _pageLayer?.RemoveView(peekView);
        }
    }

    /// <summary>
    /// The native shared-element choreography (PoC spike B): bounds, transform, image aspect
    /// and clip morph natively at display cadence. Built fresh per transaction — the fragment
    /// framework clones transitions, and a MANAGED Transition subclass loses its peer on clone.
    /// </summary>
    private static AndroidX.Transitions.Transition CreateSharedElementTransition()
    {
        var set = new AndroidX.Transitions.TransitionSet();
        set.AddTransition(new AndroidX.Transitions.ChangeBounds());
        set.AddTransition(new AndroidX.Transitions.ChangeTransform());
        set.AddTransition(new AndroidX.Transitions.ChangeImageTransform());
        set.AddTransition(new AndroidX.Transitions.ChangeClipBounds());
        set.SetDuration(_overlayDurationMs);

        return set;
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
        _currentNavBarView = null;
        _navBarPresented = false;
        _navStripAnimator = null;
        _backPreviewActive = false;
        _backPeekView = null;
        _backTopView = null;
        _backBelowPage = null;
        _predictiveHandoffPage = null;

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
                _currentBarView = barView;
                _currentTabBarArea = tabBarArea;
                _tabBarStrip.SetBar(barView.ToPlatform(mauiContext));
                freshMount = true;

                if (previousArea is not null && !ReferenceEquals(previousArea, tabBarArea))
                {
                    previousArea.OnBarViewUnmounted();
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

                await AnimateStripToAsync(strip, 0, animated).ConfigureAwait(true);

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
                await AnimateStripToAsync(stripToRemove, stripToRemove.Height, animated).ConfigureAwait(true);
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
    /// mounted while a bar view resolves (hidden = translated above the screen edge),
    /// retargeting slides, view swap only when the resolution changes.
    /// </summary>
    private Task UpdateNavBarChromeAsync(ScaffoldLayout platformView, IMauiContext mauiContext, View? navBarView, bool navBarVisible, bool animated)
    {
        if (navBarView is null)
        {
            if (_currentNavBarView is { } detachedView)
            {
                _currentNavBarView = null;
                _navBarPresented = false;
                _navBarStrip?.SetBar(null);

                if (_navBarStrip is not null)
                {
                    _navBarStrip.Visibility = ViewStates.Gone;
                }

                DetachNavBarView(detachedView);
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

        if (!ReferenceEquals(navBarView, _currentNavBarView))
        {
            if (_currentNavBarView is { } previousView)
            {
                DetachNavBarView(previousView);
            }

            _currentNavBarView = navBarView;
            navBarView.BindingContext = scaffold.NavBarContext;
            _navBarStrip.SetBar(navBarView.ToPlatform(mauiContext));

            if (animated && _lastNavStripHeight > 0)
            {
                // A freshly appearing bar starts above the edge and slides in.
                _navBarStrip.TranslationY = -_lastNavStripHeight;
            }
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

        _navBarStrip.Visibility = ViewStates.Visible;
        _navBarPresented = navBarVisible;

        if (_navBarStrip.Height > 0)
        {
            _lastNavStripHeight = _navBarStrip.Height;
        }

        if (navBarVisible)
        {
            return ShowNavAsync(_navBarStrip);
        }

        if (_lastNavStripHeight <= 0)
        {
            return Task.CompletedTask;
        }

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

            await AnimateNavStripToAsync(strip, 0, animated).ConfigureAwait(true);

            if (strip.TranslationY == 0)
            {
                strip.UnfreezeInsets();
            }
        }
    }

    private void DetachNavBarView(View navBarView)
    {
        if (ReferenceEquals(navBarView.Parent, scaffold))
        {
            scaffold.RemoveLogicalChild(navBarView);
        }
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
            {
                var navBarView = scaffold.ResolveNavBarView(page);
                var navBarVisible = navBarView is not null && Scaffold.GetIsNavBarVisible(page);

                // Same-page toggle: the page itself must relayout to the new insets.
                platformView.ChromeTopDesired = navBarVisible;
                platformView.PageTopInsetPx = navBarVisible ? _lastNavStripHeight : 0;
                RequestPageInsets();

                UpdateNavBarChromeAsync(platformView, mauiContext, navBarView, navBarVisible, animated: true).FireAndForget(scaffold.Handler);

                break;
            }
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
                var options = scaffold.GetEffectiveFlyoutOptions(request.FlyoutSide);
                var containerWidthDp = context.FromPixels(platformView.Width);
                var widthDp = options.ComputeWidth(containerWidthDp);
                var widthPx = (int)context.ToPixels(widthDp);
                var onLeft = IsFlyoutOnLeft(request.FlyoutSide);

                panel = request.Content.ToPlatform(mauiContext);
                (panel.Parent as AViewGroup)?.RemoveView(panel);

                panel.LayoutParameters = new Android.Widget.FrameLayout.LayoutParams(widthPx, AViewGroup.LayoutParams.MatchParent)
                {
                    Gravity = onLeft ? GravityFlags.Left : GravityFlags.Right
                };

                // Entrance offset rides the MAUI translation (dp), applied after mounting.
                flyoutOffscreen = onLeft ? -widthDp : widthDp;
                platformView.AddView(panel);

                break;
            }

            case ScaffoldOverlayKind.Popup:
            {
                var systemInsets = ViewCompat.GetRootWindowInsets(platformView)?.GetInsets(WindowInsetsCompat.Type.SystemBars());

                var margin = request.PopupOptions?.Margin ?? new Thickness(16);

                var area = new Rect(
                    context.FromPixels(systemInsets?.Left ?? 0) + margin.Left,
                    context.FromPixels(systemInsets?.Top ?? 0) + margin.Top,
                    Math.Max(0, context.FromPixels(platformView.Width - (systemInsets?.Left ?? 0) - (systemInsets?.Right ?? 0)) - margin.HorizontalThickness),
                    Math.Max(0, context.FromPixels(platformView.Height - (systemInsets?.Top ?? 0) - (systemInsets?.Bottom ?? 0)) - margin.VerticalThickness)
                );

                panel = request.Content.ToPlatform(mauiContext);
                (panel.Parent as AViewGroup)?.RemoveView(panel);

                panel.Measure(
                    AView.MeasureSpec.MakeMeasureSpec((int)context.ToPixels(area.Width), MeasureSpecMode.AtMost),
                    AView.MeasureSpec.MakeMeasureSpec((int)context.ToPixels(area.Height), MeasureSpecMode.AtMost)
                );

                var contentSize = new Size(
                    Math.Min(context.FromPixels(panel.MeasuredWidth), area.Width),
                    Math.Min(context.FromPixels(panel.MeasuredHeight), area.Height)
                );

                Rect? anchorBounds = null;

                if (request.PopupOptions?.Anchor is { Handler.PlatformView: AView anchorView })
                {
                    var anchorLocation = new int[2];
                    var containerLocation = new int[2];
                    anchorView.GetLocationInWindow(anchorLocation);
                    platformView.GetLocationInWindow(containerLocation);

                    anchorBounds = new Rect(
                        context.FromPixels(anchorLocation[0] - containerLocation[0]),
                        context.FromPixels(anchorLocation[1] - containerLocation[1]),
                        context.FromPixels(anchorView.Width),
                        context.FromPixels(anchorView.Height)
                    );
                }

                var rect = ScaffoldPopupPlacementResolver.Resolve(request.PopupOptions ?? new ScaffoldPopupOptions(), area, contentSize, anchorBounds, scaffold.IsRightToLeft);

                var popupLayoutParams = new Android.Widget.FrameLayout.LayoutParams((int)context.ToPixels(rect.Width), (int)context.ToPixels(rect.Height))
                {
                    Gravity = GravityFlags.Left | GravityFlags.Top,
                    LeftMargin = (int)context.ToPixels(rect.X),
                    TopMargin = (int)context.ToPixels(rect.Y)
                };

                platformView.AddView(panel, popupLayoutParams);

                break;
            }

            case ScaffoldOverlayKind.BottomSheet:
            {
                var sheet = (ScaffoldBottomSheetView)request.Content;
                var sheetInsets = ViewCompat.GetRootWindowInsets(platformView)?.GetInsets(WindowInsetsCompat.Type.SystemBars());
                var availableHeight = context.FromPixels(platformView.Height - (sheetInsets?.Top ?? 0));

                // Padding first (it affects the natural height), then measure, then geometry.
                sheet.PrepareForMeasure(context.FromPixels(sheetInsets?.Bottom ?? 0));
                panel = request.Content.ToPlatform(mauiContext);
                (panel.Parent as AViewGroup)?.RemoveView(panel);

                var sheetWidthPx = (int)Math.Min(platformView.Width, context.ToPixels(sheet.MaxWidth));

                panel.Measure(
                    AView.MeasureSpec.MakeMeasureSpec(sheetWidthPx, MeasureSpecMode.Exactly),
                    AView.MeasureSpec.MakeMeasureSpec((int)context.ToPixels(availableHeight), MeasureSpecMode.AtMost)
                );

                var natural = Math.Min(context.FromPixels(panel.MeasuredHeight), availableHeight);
                var sheetHeight = sheet.InitializeGeometry(availableHeight, natural);

                // Bottom-anchored, centered at the (possibly capped) width; the sheet's own
                // TranslationY does the rest.
                var sheetLayoutParams = new Android.Widget.FrameLayout.LayoutParams(sheetWidthPx, (int)context.ToPixels(sheetHeight))
                {
                    Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal
                };

                platformView.AddView(panel, sheetLayoutParams);

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

        await previous.Content.FadeTo(0, 100);
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

        await ScaffoldOverlayAnimations.ExitAsync(request, entry.ScrimView, entry.FlyoutOffscreenTranslation);

        (entry.ContentPlatform.Parent as AViewGroup)?.RemoveView(entry.ContentPlatform);
        (entry.ScrimPlatform.Parent as AViewGroup)?.RemoveView(entry.ScrimPlatform);
        _overlays.Remove(entry);

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
