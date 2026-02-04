using UIKit;

namespace Nalu;

/// <summary>
/// A container view controller that manages multiple child view controllers,
/// displaying only one at a time while keeping all children active to preserve their state.
/// Similar to UITabBarController behavior where scroll positions and other state are maintained.
/// </summary>
internal class NaluShellSectionWrapperController : UIViewController
{
    private readonly HashSet<UIViewController> _registeredViewControllers = [];
    
    public UIViewController? SelectedViewController
    {
        get;
        set
        {
            var oldValue = field;

            if (value is not null && !_registeredViewControllers.Contains(value))
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

    public void AddViewController(UIViewController viewController) => _registeredViewControllers.Add(viewController);

    public void RemoveViewController(UIViewController viewController)
    {
        if (ReferenceEquals(SelectedViewController, viewController))
        {
            SelectedViewController = null;
        }

        if (viewController.ParentViewController is not null)
        {
            viewController.WillMoveToParentViewController(null!);
            viewController.View!.RemoveFromSuperview();
            viewController.RemoveFromParentViewController();
        }
        
        _registeredViewControllers.Remove(viewController);
    }

    private void SelectViewController(UIViewController? oldViewController, UIViewController? newViewController)
    {
        if (newViewController is not null)
        {
            AddChildViewController(newViewController);
        }

        if (oldViewController is not null)
        {
            oldViewController.WillMoveToParentViewController(null);
        }

        if (newViewController is not null)
        {
            var childView = newViewController.View!;
            View!.InsertSubview(childView, 0);
            childView.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints([
                childView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
                childView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
                childView.LeftAnchor.ConstraintEqualTo(View.LeftAnchor),
                childView.RightAnchor.ConstraintEqualTo(View.RightAnchor)
            ]);
        }
        
        if (oldViewController is not null)
        {
            oldViewController.View!.RemoveFromSuperview();
            oldViewController.RemoveFromParentViewController();
        }

        if (newViewController is not null)
        {
            newViewController.DidMoveToParentViewController(this);
        }
    }
}

