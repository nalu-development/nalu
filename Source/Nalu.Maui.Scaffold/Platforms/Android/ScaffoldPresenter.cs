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

    // Provisional flyout metrics (flyout width/styling API is a pending design review).
    private const double _flyoutWidthRatio = 0.85;
    private const double _flyoutMaxWidthDp = 360;
    private static readonly Color _flyoutScrimColor = Colors.Black.WithAlpha(0.4f);

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

    private AView? _overlayScrim;
    private AView? _overlayPanel;
    private View? _overlayContent;
    private ScaffoldOverlayPlacement _overlayPlacement;
    private Action? _overlayCleanup;

    private ScaffoldRoot? _currentRoot;
    private AView? _backPeekView;
    private AView? _backTopView;
    private Page? _backBelowPage;
    private bool _backPreviewActive;
    private Page? _predictiveHandoffPage;

    public bool HasOverlay => _overlayPanel is not null;

    /// <summary>Whether a predictive-back preview is currently scrubbing.</summary>
    public bool HasBackPreview => _backPreviewActive;

    private enum ScaffoldOverlayPlacement
    {
        FlyoutStart,
        FlyoutEnd,
        AboveBottomChrome
    }

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
        await CloseOverlayAsync();

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

            var previousFragment = _currentFragment;
            var fragment = new ScaffoldPageFragment(mauiContext, targetPage, hint, container, postponeForSharedElements: sharedNames.Count > 0);
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

                foreach (var name in sharedNames)
                {
                    var sharedPlatformView = outgoingTagged[name].ToPlatform(mauiContext);
                    ViewCompat.SetTransitionName(sharedPlatformView, name);
                    transaction.AddSharedElement(sharedPlatformView, name);
                }
            }

            // The CURRENT navigation decides how the outgoing page leaves (a pop slides it out
            // to the end edge; a push leaves it static beneath the incoming slide). A settled
            // predictive-back preview already moved it offscreen — no exit animation.
            previousFragment?.PrepareRemoval(predictivelySettled ? ScaffoldPresentationHint.None : hint);

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

            return AnimateStripToAsync(_tabBarStrip, 0, animated);
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

        return _lastStripHeight > 0
            ? AnimateStripToAsync(hiddenStrip, _lastStripHeight, animated)
            : Task.CompletedTask;

        async Task UnmountAsync(ScaffoldTabBarStripLayout stripToRemove, ScaffoldTabBar? previousArea)
        {
            if (stripToRemove.Height > 0)
            {
                _lastStripHeight = stripToRemove.Height;
                await AnimateStripToAsync(stripToRemove, stripToRemove.Height, animated).ConfigureAwait(true);
            }

            stripToRemove.Visibility = ViewStates.Gone;
            stripToRemove.TranslationY = 0;
            stripToRemove.SetBar(null);
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
            return AnimateNavStripToAsync(_navBarStrip, 0, animated);
        }

        return _lastNavStripHeight > 0
            ? AnimateNavStripToAsync(_navBarStrip, -_lastNavStripHeight, animated)
            : Task.CompletedTask;
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

    public Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content)
        => ShowOverlayAsync(
            content,
            side == ScaffoldFlyoutSide.Start ? ScaffoldOverlayPlacement.FlyoutStart : ScaffoldOverlayPlacement.FlyoutEnd,
            _flyoutScrimColor,
            behindBottomChrome: false,
            disconnectOnClose: false
        );

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
            || scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext }
            || platformView.Context is not { } context)
        {
            return;
        }

        var chromeLayer = behindBottomChrome && _tabBarStrip is { Visibility: ViewStates.Visible } strip ? strip : null;
        var chromeLayerIndex = chromeLayer is null ? -1 : platformView.IndexOfChild(chromeLayer);
        var excludedBottom = behindBottomChrome ? platformView.ChromeBottomFootprint : 0;

        var scrim = new AView(context) { Clickable = true, Alpha = 0 };
        scrim.SetBackgroundColor(scrimColor.ToPlatform());
        scrim.Click += (_, _) => _ = CloseOverlayAsync();

        var scrimLayoutParams = new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent);

        if (chromeLayerIndex >= 0)
        {
            platformView.AddView(scrim, chromeLayerIndex++, scrimLayoutParams);
        }
        else
        {
            platformView.AddView(scrim, scrimLayoutParams);
        }

        var panel = content.ToPlatform(mauiContext);
        (panel.Parent as AViewGroup)?.RemoveView(panel);

        _overlayScrim = scrim;
        _overlayPanel = panel;
        _overlayContent = disconnectOnClose ? content : null;
        _overlayPlacement = placement;
        scaffold.UpdateBackCallbackEnabled();

        switch (placement)
        {
            case ScaffoldOverlayPlacement.FlyoutStart:
            case ScaffoldOverlayPlacement.FlyoutEnd:
            {
                var widthPx = (int)Math.Min(platformView.Width * _flyoutWidthRatio, context.ToPixels(_flyoutMaxWidthDp));

                panel.LayoutParameters = new Android.Widget.FrameLayout.LayoutParams(widthPx, AViewGroup.LayoutParams.MatchParent)
                {
                    Gravity = placement == ScaffoldOverlayPlacement.FlyoutStart ? GravityFlags.Start : GravityFlags.End
                };
                panel.TranslationX = placement == ScaffoldOverlayPlacement.FlyoutStart ? -widthPx : widthPx;
                platformView.AddView(panel);

                await AnimateOverlayAsync(scrim, scrimAlpha: 1, panel, panelProperty: "translationX", panelTarget: 0);

                break;
            }

            case ScaffoldOverlayPlacement.AboveBottomChrome:
            {
                var gapPx = (int)context.ToPixels(_overflowGapDp);
                var margin = content.Margin;

                // The panel hugs its content and centers, mirroring the bar pill's own sizing.
                var panelLayoutParams = new Android.Widget.FrameLayout.LayoutParams(AViewGroup.LayoutParams.WrapContent, AViewGroup.LayoutParams.WrapContent)
                {
                    Gravity = GravityFlags.Bottom | GravityFlags.CenterHorizontal,
                    LeftMargin = (int)context.ToPixels(margin.Left),
                    RightMargin = (int)context.ToPixels(margin.Right),
                    BottomMargin = excludedBottom + gapPx
                };
                panel.Alpha = 0;
                panel.TranslationY = context.ToPixels(24);

                if (chromeLayerIndex >= 0)
                {
                    platformView.AddView(panel, chromeLayerIndex, panelLayoutParams);
                }
                else
                {
                    platformView.AddView(panel, panelLayoutParams);
                }

                await AnimateOverlayAsync(scrim, scrimAlpha: 1, panel, panelProperty: "translationY", panelTarget: 0, alsoFadePanel: true);

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

        switch (placement)
        {
            case ScaffoldOverlayPlacement.FlyoutStart:
                await AnimateOverlayAsync(scrim, scrimAlpha: 0, panel, panelProperty: "translationX", panelTarget: -panel.Width);

                break;

            case ScaffoldOverlayPlacement.FlyoutEnd:
                await AnimateOverlayAsync(scrim, scrimAlpha: 0, panel, panelProperty: "translationX", panelTarget: panel.Width);

                break;

            case ScaffoldOverlayPlacement.AboveBottomChrome:
                await AnimateOverlayAsync(scrim, scrimAlpha: 0, panel, panelProperty: "translationY", panelTarget: panel.Context is { } ctx ? ctx.ToPixels(24) : 24, alsoFadePanel: true);

                break;
        }

        (panel.Parent as AViewGroup)?.RemoveView(panel);
        (scrim.Parent as AViewGroup)?.RemoveView(scrim);

        if (content is not null)
        {
            content.DisconnectHandlers();
        }

        scaffold.UpdateBackCallbackEnabled();
    }

    private static Task AnimateOverlayAsync(AView scrim, float scrimAlpha, AView panel, string panelProperty, float panelTarget, bool alsoFadePanel = false)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var current = panelProperty == "translationX" ? panel.TranslationX : panel.TranslationY;
        var panelAnimator = Android.Animation.ObjectAnimator.OfFloat(panel, panelProperty, current, panelTarget)!;
        panelAnimator.SetDuration(_overlayDurationMs);
        panelAnimator.AnimationEnd += (_, _) => completion.TrySetResult();

        var scrimAnimator = Android.Animation.ObjectAnimator.OfFloat(scrim, "alpha", scrim.Alpha, scrimAlpha)!;
        scrimAnimator.SetDuration(_overlayDurationMs);

        if (alsoFadePanel)
        {
            var panelFadeAnimator = Android.Animation.ObjectAnimator.OfFloat(panel, "alpha", panel.Alpha, scrimAlpha)!;
            panelFadeAnimator.SetDuration(_overlayDurationMs);
            panelFadeAnimator.Start();
        }

        panelAnimator.Start();
        scrimAnimator.Start();

        return completion.Task;
    }
}
