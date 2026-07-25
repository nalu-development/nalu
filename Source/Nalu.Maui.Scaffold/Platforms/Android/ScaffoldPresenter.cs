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

    private AView? _overlayScrim;
    private AView? _overlayPanel;
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
        if (scaffold.Handler is not IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext } ||
            platformView.Context?.GetActivity() is not AppCompatActivity activity)
        {
            return;
        }

        scaffold.EnsureBackCallback(activity);

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
        var animated = hint != ScaffoldPresentationHint.None;

        // Inset intent BEFORE the fragment commit: the incoming page attaches with its final
        // insets while the outgoing page keeps its stale layout — no jumps during transitions.
        platformView.ChromeBottomDesired = barVisible;
        platformView.PageBottomInsetPx = barVisible ? _lastStripHeight : 0;

        // Chrome and page animate CONCURRENTLY: an Auto-hiding bar slides away while the pushed
        // page slides in (and back in sync on pop).
        var chromeTask = UpdateTabBarChromeAsync(platformView, mauiContext, tabBarArea, barVisible, animated);

        if (!ReferenceEquals(targetPage, _currentPage))
        {
            if (_currentPage is not null)
            {
                _currentPage.PropertyChanged -= OnCurrentPagePropertyChanged;
            }

            var previousFragment = _currentFragment;
            var fragment = new ScaffoldPageFragment(mauiContext, targetPage, hint, container);
            _currentFragment = fragment;
            _currentPage = targetPage;
            targetPage.PropertyChanged += OnCurrentPagePropertyChanged;

            // Async commit only: a synchronous commit can run while MAUI's own ScopedFragment
            // transaction is still executing on the same FragmentManager ("already executing").
            activity.SupportFragmentManager
                    .BeginTransaction()
                    .SetReorderingAllowed(true)
                    .Replace(container.Id, fragment)
                    .CommitAllowingStateLoss();

            // Deterministic completion: presentation of the new page plus dismissal animation of
            // the previous one, with a settle timeout as a safety net.
            var settled = Task.WhenAll(fragment.PresentedTask, previousFragment?.DismissedTask ?? Task.CompletedTask);
            await Task.WhenAny(settled, Task.Delay(_settleTimeoutMs)).ConfigureAwait(true);
        }

        await chromeTask.ConfigureAwait(true);
        scaffold.UpdateBackCallbackEnabled();
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
    /// Brings the chrome to the desired state: mounts the strip and bar view, slides the strip
    /// in/out (concurrently with the page transition), and unmounts the bar view on hide so the
    /// element tree reflects presented chrome.
    /// </summary>
    private async Task UpdateTabBarChromeAsync(ScaffoldLayout platformView, IMauiContext mauiContext, ScaffoldTabBar? tabBarArea, bool barVisible, bool animated)
    {
        if (tabBarArea is not null && barVisible)
        {
            var barView = tabBarArea.GetOrCreateBarView();
            var wasHidden = _currentBarView is null;

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

            if (!ReferenceEquals(barView, _currentBarView))
            {
                var previousArea = _currentTabBarArea;
                _currentBarView = barView;
                _currentTabBarArea = tabBarArea;
                _tabBarStrip.SetBar(barView.ToPlatform(mauiContext));

                if (previousArea is not null && !ReferenceEquals(previousArea, tabBarArea))
                {
                    previousArea.OnBarViewUnmounted();
                }
            }

            _tabBarStrip.Visibility = ViewStates.Visible;

            if (wasHidden && animated && _lastStripHeight > 0)
            {
                // A freshly appearing bar starts below the edge and slides in with the pop.
                _tabBarStrip.TranslationY = _lastStripHeight;
                await AnimateTranslationYAsync(_tabBarStrip, 0).ConfigureAwait(true);
            }
            else
            {
                _tabBarStrip.TranslationY = 0;
            }

            if (_tabBarStrip.Height > 0)
            {
                _lastStripHeight = _tabBarStrip.Height;
            }

            return;
        }

        if (_currentBarView is not null && _tabBarStrip is { } strip)
        {
            var previousArea = _currentTabBarArea;
            _currentBarView = null;
            _currentTabBarArea = null;

            if (strip.Height > 0)
            {
                _lastStripHeight = strip.Height;
            }

            if (animated && strip.Height > 0)
            {
                await AnimateTranslationYAsync(strip, strip.Height).ConfigureAwait(true);
            }

            strip.Visibility = ViewStates.Gone;
            strip.TranslationY = 0;
            strip.SetBar(null);

            // The element tree reflects presented chrome: detach the bar view on unmount.
            previousArea?.OnBarViewUnmounted();
        }
    }

    private static Task AnimateTranslationYAsync(AView view, float target)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var animator = Android.Animation.ObjectAnimator.OfFloat(view, "translationY", view.TranslationY, target)!;
        animator.SetDuration(_overlayDurationMs);
        animator.AnimationEnd += (_, _) => completion.TrySetResult();
        animator.Start();

        return completion.Task;
    }

    private void OnCurrentPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Bar visibility is an animated inset change, not a page relayout (§5.4).
        if (e.PropertyName == "TabBarVisibility"
            && sender is Page page
            && ReferenceEquals(page, _currentPage)
            && scaffold.Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy rootProxy
            && scaffold.Handler is IPlatformViewHandler { PlatformView: ScaffoldLayout platformView, MauiContext: { } mauiContext })
        {
            var tabBarArea = rootProxy.Root.Parent as ScaffoldTabBar;
            var barVisible = tabBarArea is not null && Scaffold.ComputeTabBarVisible(rootProxy.Root, page);

            // Same-page toggle: the page itself must relayout to the new insets.
            platformView.ChromeBottomDesired = barVisible;
            platformView.PageBottomInsetPx = barVisible ? _lastStripHeight : 0;

            if (_pageLayer is { } pageLayer)
            {
                ViewCompat.RequestApplyInsets(pageLayer);
            }

            UpdateTabBarChromeAsync(platformView, mauiContext, tabBarArea, barVisible, animated: true).FireAndForget(scaffold.Handler);
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
