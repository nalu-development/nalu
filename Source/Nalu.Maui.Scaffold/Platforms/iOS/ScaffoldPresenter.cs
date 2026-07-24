using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// iOS presenter (P0): hosts the visible page as a child UIViewController of the scaffold
/// page's own controller (UIKit containment — safe area and appearance callbacks propagate),
/// synchronizing to the stack model with a minimal slide transition.
/// Single-visible-page policy: covered pages are unmounted and remounted on reveal.
/// The full transition engine (shared elements, interactive pop) arrives with P2.
/// </summary>
internal sealed class ScaffoldPresenter(Scaffold scaffold) : IScaffoldPresenter
{
    private const double _transitionDurationSeconds = 0.25;

    // Provisional chrome metrics (final styling surface arrives with the P1 API review).
    private const double _flyoutWidthRatio = 0.85;
    private const double _flyoutMaxWidth = 360;
    private const float _flyoutScrimAlpha = 0.4f;

    private Page? _currentPage;
    private UIViewController? _currentController;
    private UIView? _flyoutScrim;
    private UIView? _flyoutPanel;
    private ScaffoldFlyoutSide _flyoutSide;

    public async Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
    {
        if (scaffold.Handler is not IPlatformViewHandler { ViewController: { } parentController, PlatformView: { } container, MauiContext: { } mauiContext })
        {
            return;
        }

        // Navigation dismisses any open flyout.
        await CloseFlyoutAsync();

        var stack = root.NavigationStack;
        var targetPage = stack.PushedPages.Count > 0 ? stack.PushedPages[^1].Page : stack.RootPage;

        if (targetPage is null || ReferenceEquals(targetPage, _currentPage))
        {
            return;
        }

        var previousController = _currentController;
        var newController = targetPage.ToUIViewController(mauiContext);
        _currentPage = targetPage;
        _currentController = newController;

        parentController.AddChildViewController(newController);
        var newView = newController.View!;
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

            default:
                container.AddSubview(newView);
                newController.DidMoveToParentViewController(parentController);

                break;
        }

        if (previousController is not null)
        {
            previousController.WillMoveToParentViewController(null);
            previousController.View?.RemoveFromSuperview();
            previousController.RemoveFromParentViewController();
        }
    }

    public async Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content)
    {
        if (_flyoutPanel is not null
            || scaffold.Handler is not IPlatformViewHandler { PlatformView: { } container, MauiContext: { } mauiContext })
        {
            return;
        }

        var containerWidth = container.Bounds.Width;
        var containerHeight = container.Bounds.Height;
        var width = Math.Min(containerWidth * _flyoutWidthRatio, _flyoutMaxWidth);

        var scrim = new UIView(container.Bounds)
        {
            BackgroundColor = UIColor.Black,
            Alpha = 0,
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight
        };
        scrim.AddGestureRecognizer(new UITapGestureRecognizer(() => _ = CloseFlyoutAsync()));
        container.AddSubview(scrim);

        var panel = content.ToPlatform(mauiContext);
        var offscreenX = side == ScaffoldFlyoutSide.Start ? -width : containerWidth;
        var openX = side == ScaffoldFlyoutSide.Start ? 0 : containerWidth - width;
        panel.Frame = new CGRect(offscreenX, 0, width, containerHeight);
        container.AddSubview(panel);

        _flyoutScrim = scrim;
        _flyoutPanel = panel;
        _flyoutSide = side;

        await UIView.AnimateAsync(_transitionDurationSeconds, () =>
        {
            scrim.Alpha = _flyoutScrimAlpha;
            panel.Frame = new CGRect(openX, 0, width, containerHeight);
        });
    }

    public async Task CloseFlyoutAsync()
    {
        if (_flyoutPanel is not { } panel || _flyoutScrim is not { } scrim)
        {
            return;
        }

        _flyoutPanel = null;
        _flyoutScrim = null;

        var containerWidth = panel.Superview?.Bounds.Width ?? panel.Frame.Width;
        var offscreenX = _flyoutSide == ScaffoldFlyoutSide.Start ? -panel.Frame.Width : containerWidth;

        await UIView.AnimateAsync(_transitionDurationSeconds, () =>
        {
            scrim.Alpha = 0;
            panel.Frame = new CGRect(offscreenX, panel.Frame.Y, panel.Frame.Width, panel.Frame.Height);
        });

        panel.RemoveFromSuperview();
        scrim.RemoveFromSuperview();
    }
}
