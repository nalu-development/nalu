using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using UIKit;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

/// <summary>
/// Custom Shell Item Renderer that prevents iOS's UITabBarController from showing
/// the "More" navigation controller when there are more than 5 tabs.
/// 
/// With this implementation:
/// - All tab ShellSectionRenderers are forced to never be marked as "IsInMoreTab"
/// - All the user's tabs (any number) are available through the custom tab bar UI,
///   while only up to 5 are exposed to UITabBarController's ViewControllers property
/// - When a tab beyond the 5 UITabBarController slots is selected, the implementation
///   swaps that view controller into the visible array, replacing a currently visible tab
/// - Eviction for replacement is handled by simply rotating out the least recently active visible tab
/// - UITabBarController behaviors (lifecycle, layout, safe areas, etc.) are left fully intact
/// - iOS's default "More" controller will never appear or be reachable by the user, regardless of tab count
/// 
/// This approach lets NaluShell provide unlimited custom tab bar experiences on iOS,
/// avoiding the system "More" tab UI entirely.
/// </summary>
public class NaluShellItemRenderer : ShellItemRenderer
{
    private const nint _mauiTabBarTag = 0x63D2AF;
    private const int _maxViewControllersToAvoidMore = 5;
    private const int _maxViewControllersWhenMoreIsActive = 4;
    
    private readonly IShellContext _shellContext;
    private UIView? _platformTabBar;
    private View? _crossPlatformTabBar;
    private bool _hasCustomTabBarView;

    public new ShellItem ShellItem
    {
        get => base.ShellItem;
        set
        {
            base.ShellItem = value;
            UpdateTabBarView();
        }
    }

    public NaluShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
        _shellContext = shellContext;
    }
    
    // UnsafeAccessor methods to access private/internal members of base ShellItemRenderer
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_sectionRenderers")]
    private static extern ref Dictionary<UIViewController, IShellSectionRenderer> GetSectionRenderers(ShellItemRenderer instance);
    
    public override UIViewController? SelectedViewController
    {
        get => base.SelectedViewController;
        set
        {
            if (_hasCustomTabBarView)
            {
                var newSelectedViewController = value!;
                var oldSelectedViewController = base.SelectedViewController!;
                var viewControllers = ViewControllers!;

                var newSelectedIndex = Array.IndexOf(viewControllers, newSelectedViewController);

                if (viewControllers.Length >= _maxViewControllersToAvoidMore &&
                    newSelectedIndex >= _maxViewControllersWhenMoreIsActive)
                {
                    var oldSelectedIndex = Array.IndexOf(viewControllers, oldSelectedViewController);
                    // Get the nearest unselected index to swap out
                    var unselectedIndex = ++oldSelectedIndex % _maxViewControllersWhenMoreIsActive;
                    // Create a new array with the swapped view controllers
                    var newViewControllers = new UIViewController[viewControllers.Length];
                    Array.Copy(viewControllers, newViewControllers, viewControllers.Length);
                    // Swap the new selected controller with the unselected one
                    newViewControllers[unselectedIndex] = newSelectedViewController;
                    newViewControllers[newSelectedIndex] = viewControllers[unselectedIndex];
                    // Update the ViewControllers array
                    base.ViewControllers = newViewControllers;
                    UpdateTabBarView();
                }
            }

            base.SelectedViewController = value;
        }
    }

    protected override void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsCollectionChanged(sender, e);
        UpdateTabBarView();
    }
    
    private void EnsureNoMoreTabRenderers(bool forceNotInMoreTab)
    {
        // Use UnsafeAccessor to access the section renderers dictionary from base class
        ref var sectionRenderers = ref GetSectionRenderers(this);

        var viewControllers = ViewControllers!;
        var willUseMoreTab = viewControllers.Length > _maxViewControllersToAvoidMore;
        for (var i = 0; i < viewControllers.Length; i++)
        {
            if (sectionRenderers.TryGetValue(viewControllers[i], out var renderer))
            {
                var isMainTab = forceNotInMoreTab || !willUseMoreTab || i < _maxViewControllersToAvoidMore - 1;
                renderer.IsInMoreTab =  !isMainTab;
            }
        }
    }

    public void UpdateTabBarView()
    {
        // Get the TabBarView from the ShellItem
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        if (tabBarView == null)
        {
            // Remove custom view if it was previously added
            _crossPlatformTabBar?.DisconnectHandlers();
            _platformTabBar?.RemoveFromSuperview();
            _platformTabBar = null;
            _crossPlatformTabBar = null;
            _hasCustomTabBarView = false;

            // Show native tab bar items (in case they were hidden)
            HideNativeTabBarItems(false);
            EnsureNoMoreTabRenderers(false);
        }
        else
        {
            _hasCustomTabBarView = true;

            if (_crossPlatformTabBar != tabBarView)
            {
                _crossPlatformTabBar?.DisconnectHandlers();
                _platformTabBar?.RemoveFromSuperview();

                var mauiContext = _shellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
                var platformView = tabBarView.ToPlatform(mauiContext);
                platformView.Tag = _mauiTabBarTag;
                platformView.TranslatesAutoresizingMaskIntoConstraints = false;
                _platformTabBar = platformView;
                _crossPlatformTabBar = tabBarView;
                TabBar.AddSubview(_platformTabBar);

                NSLayoutConstraint.ActivateConstraints(
                    [
                        platformView.LeadingAnchor.ConstraintEqualTo(TabBar.LeadingAnchor),
                        platformView.TrailingAnchor.ConstraintEqualTo(TabBar.TrailingAnchor),
                        platformView.BottomAnchor.ConstraintEqualTo(TabBar.BottomAnchor),
                        platformView.TopAnchor.ConstraintEqualTo(TabBar.TopAnchor)
                    ]
                );
            }
            EnsureNoMoreTabRenderers(true);
            HideNativeTabBarItems(true);
        }
    }

    private void HideNativeTabBarItems(bool hidden)
    {
        // Show/hide all subviews that are not our custom MAUI bar
        foreach (var subview in TabBar.Subviews)
        {
            if (subview.Tag != _mauiTabBarTag)
            {
                subview.Hidden = hidden;
            }
        }

        // Disable/Enable liquid glass gestures on the native tab bar
        if (TabBar.GestureRecognizers is { } gestureRecognizers)
        {
            foreach (var gestureRecognizer in gestureRecognizers)
            {
                gestureRecognizer.Enabled = !hidden;
            }
        }
    }
}
