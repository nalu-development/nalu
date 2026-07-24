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
    private const double TransitionDurationSeconds = 0.25;

    private Page? _currentPage;
    private UIViewController? _currentController;

    public async Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
    {
        if (scaffold.Handler is not IPlatformViewHandler { ViewController: { } parentController } handler
            || handler.PlatformView is not { } container
            || handler.MauiContext is not { } mauiContext)
        {
            return;
        }

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
                await UIView.AnimateAsync(TransitionDurationSeconds, () => newView.Transform = CGAffineTransform.MakeIdentity());

                break;

            case ScaffoldPresentationHint.Pop when previousController?.View is { } previousView:
                container.InsertSubviewBelow(newView, previousView);
                newController.DidMoveToParentViewController(parentController);
                await UIView.AnimateAsync(TransitionDurationSeconds, () => previousView.Transform = CGAffineTransform.MakeTranslation(width, 0));

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
}
