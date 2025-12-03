using UIKit;

namespace Nalu;

/// <summary>
/// A container view controller that manages multiple child view controllers,
/// displaying only one at a time while keeping all children active to preserve their state.
/// Similar to UITabBarController behavior where scroll positions and other state are maintained.
/// </summary>
internal class NaluShellSectionWrapperController : UIViewController
{
    public UIViewController? SelectedViewController
    {
        get;
        set
        {
            var oldValue = field;

            if (value is not null && !ChildViewControllers.Contains(value))
            {
                throw new InvalidOperationException($"{nameof(SelectedViewController)} must be one of the child view controllers.");
            }
            
            if (!ReferenceEquals(oldValue, value))
            {
                field = value;
                SelectViewController(oldValue, value);
            }
        }
    }

    public void AddViewController(UIViewController viewController)
    {
        AddChildViewController(viewController);
        var viewControllerView = viewController.View!;
        viewControllerView.Hidden = true;
        View!.AddSubview(viewControllerView);
        viewController.DidMoveToParentViewController(this);
    }

    public void RemoveViewController(UIViewController viewController)
    {
        if (ReferenceEquals(SelectedViewController, viewController))
        {
            SelectedViewController = null;
        }
        
        viewController.WillMoveToParentViewController(null);
        viewController.View!.RemoveFromSuperview();
        viewController.RemoveFromParentViewController();
    }

    private void SelectViewController(UIViewController? oldViewController, UIViewController? newViewController)
    {
        if (oldViewController is not null)
        {
            var oldView = oldViewController.View!;

            if (newViewController is null)
            {
                oldViewController.BeginAppearanceTransition(false, false);
                oldView.Hidden = true;
                oldViewController.EndAppearanceTransition();
                return;
            }

            var newView = newViewController.View!;
            newView.Hidden = true;
            
            oldViewController.BeginAppearanceTransition(false, false);
            newViewController.BeginAppearanceTransition(true, false);

            if (newView.Superview is null)
            {
                newView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
                View!.AddSubview(newView);
            }
            
            oldView.Hidden = true;
            newView.Hidden = false;
            
            oldViewController.EndAppearanceTransition();
            newViewController.EndAppearanceTransition();
            return;
        }
        
        if (newViewController is not null)
        {
            var newView = newViewController.View!;
            newView.Hidden = true;

            newViewController.BeginAppearanceTransition(true, false);

            if (newView.Superview is null)
            {
                View!.AddSubview(newView);
            }

            newView.Hidden = false;

            newViewController.EndAppearanceTransition();
        }
    }
}

