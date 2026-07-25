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

    // Provisional flyout metrics (flyout width/styling API is a pending design review).
    private const double _flyoutWidthRatio = 0.85;
    private const double _flyoutMaxWidth = 360;
    private static readonly Color _flyoutScrimColor = Colors.Black.WithAlpha(0.4f);

    private Page? _currentPage;
    private UIViewController? _currentController;
    private ScaffoldTabBar? _currentTabBarArea;
    private View? _currentBarView;

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
        var animated = hint != ScaffoldPresentationHint.None;

        // Chrome and page animate CONCURRENTLY: an Auto-hiding bar slides away while the pushed
        // page slides in (and back in sync on pop) — no sequential two-phase motion.
        var chromeTask = UpdateTabBarChromeAsync(controller, mauiContext, tabBarArea, barVisible, animated);

        var pageTask = ReferenceEquals(targetPage, _currentPage)
            ? Task.CompletedTask
            : TransitionToPageAsync(controller, mauiContext, targetPage, hint, barVisible);

        await Task.WhenAll(chromeTask, pageTask);
    }

    private async Task TransitionToPageAsync(ScaffoldViewController controller, IMauiContext mauiContext, Page targetPage, ScaffoldPresentationHint hint, bool barVisible)
    {
        var parentController = controller.ContentHost;
        var container = controller.ContentContainer;

        if (_currentPage is not null)
        {
            _currentPage.PropertyChanged -= OnCurrentPagePropertyChanged;
        }

        var previousController = _currentController;
        var newController = targetPage.ToUIViewController(mauiContext);
        _currentPage = targetPage;
        _currentController = newController;
        targetPage.PropertyChanged += OnCurrentPagePropertyChanged;

        // §5.4 per-page inset application: each page is laid out with the insets matching its
        // own bar visibility from birth — the outgoing page keeps its insets while leaving.
        controller.CurrentPageController = newController;
        controller.CurrentPageWantsBarInset = barVisible;
        newController.AdditionalSafeAreaInsets = barVisible
            ? new UIEdgeInsets(0, 0, controller.BarHeight, 0)
            : UIEdgeInsets.Zero;

        parentController.AddChildViewController(newController);
        var newView = newController.View!;

        // A remounted page keeps the transform its unmount animation left behind (covered pages
        // are detached, never destroyed) — setting Frame under an active transform corrupts the
        // geometry (the page lands offscreen). Always clear before framing.
        newView.Transform = CGAffineTransform.MakeIdentity();
        newView.Frame = container.Bounds;
        newView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

        var width = container.Bounds.Width;

        switch (hint)
        {
            case ScaffoldPresentationHint.Push:
                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);
                newView.Transform = CGAffineTransform.MakeTranslation(width, 0);
                await UIView.AnimateAsync(_transitionDurationSeconds, () => newView.Transform = CGAffineTransform.MakeIdentity());

                break;

            case ScaffoldPresentationHint.Pop when previousController?.View is { } previousView:
                container.InsertSubviewBelow(newView, previousView);
                newController.DidMoveToParentViewController(parentController);
                await UIView.AnimateAsync(_transitionDurationSeconds, () => previousView.Transform = CGAffineTransform.MakeTranslation(width, 0));

                break;

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

                // Leave the detached view transform-clean for its next mount.
                previousView.Transform = CGAffineTransform.MakeIdentity();
            }

            previousController.RemoveFromParentViewController();
        }
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
        // Bar visibility is an animated inset change, not a page relayout (§5.4).
        if (e.PropertyName == "TabBarVisibility"
            && sender is Page page
            && ReferenceEquals(page, _currentPage)
            && scaffold.Proxy?.CurrentItem.CurrentSection is ScaffoldRootProxy rootProxy
            && scaffold.Handler is IPlatformViewHandler { ViewController: ScaffoldViewController controller, MauiContext: { } mauiContext })
        {
            var tabBarArea = rootProxy.Root.Parent as ScaffoldTabBar;
            var barVisible = tabBarArea is not null && Scaffold.ComputeTabBarVisible(rootProxy.Root, page);
            controller.CurrentPageWantsBarInset = barVisible;
            UpdateTabBarChromeAsync(controller, mauiContext, tabBarArea, barVisible, animated: true).FireAndForget(scaffold.Handler);
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
                var width = Math.Min(bounds.Width * _flyoutWidthRatio, _flyoutMaxWidth);
                var offscreenX = placement == ScaffoldOverlayPlacement.FlyoutStart ? -width : bounds.Width;
                var openX = placement == ScaffoldOverlayPlacement.FlyoutStart ? 0 : bounds.Width - width;
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
                    panel.Frame = panel.Frame with { X = -panel.Frame.Width };

                    break;

                case ScaffoldOverlayPlacement.FlyoutEnd:
                    panel.Frame = panel.Frame with { X = containerWidth };

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
    }
}
