using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// Custom Shell Item Renderer that prevents iOS's UITabBarController from showing
/// the "More" navigation controller when there are more than 5 tabs.
/// 
/// Strategy:
/// - Stores all view controllers internally in _allViewControllers
/// - Only exposes the first 5 view controllers to the base UITabBarController
/// - When tabs beyond index 5 are selected, dynamically swaps them into the ViewControllers array
/// - Uses LRU (Least Recently Used) eviction to determine which controller to swap out
/// - Lets UITabBarController handle all view lifecycle, layout, and safe area management
/// - Ensures all ShellSectionRenderers have IsInMoreTab = false to prevent redirection
/// 
/// This allows custom tab bars to handle unlimited tabs without iOS's More controller interfering.
/// </summary>
internal class NaluShellItemRenderer : ShellItemRenderer
{
    private const nint _mauiTabBarTag = 0x63D2AF;
    private const int _maxViewControllersToAvoidMore = 5;
    
    private readonly IShellContext _shellContext;
    private UIView? _platformTabBar;
    private View? _crossPlatformTabBar;

    public new ShellItem ShellItem
    {
        get => base.ShellItem;
        set
        {
            base.ShellItem = value;
            EnsureNoMoreTabRenderers();
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
    private static extern ref Dictionary<UIViewController, IShellSectionRenderer>? GetSectionRenderers(ShellItemRenderer instance);
    
    public override UIViewController? SelectedViewController
    {
        get => base.SelectedViewController;
        set
        {
            var newSelectedViewController = value!;
            var oldSelectedViewController = base.SelectedViewController!;
            var viewControllers = ViewControllers!;
            
            var newSelectedIndex = Array.IndexOf(viewControllers, newSelectedViewController);

            if (newSelectedIndex >= _maxViewControllersToAvoidMore)
            {
                var oldSelectedIndex = Array.IndexOf(viewControllers, oldSelectedViewController);
                // Get the nearest unselected index to swap out
                var unselectedIndex = ++oldSelectedIndex % _maxViewControllersToAvoidMore;
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

            base.SelectedViewController = value;
        }
    }

    protected override void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsCollectionChanged(sender, e);
        
        // After base call, ensure all renderers have IsInMoreTab = false
        // since we're preventing the More controller from being used
        EnsureNoMoreTabRenderers();
        UpdateTabBarView();
    }
    
    private void EnsureNoMoreTabRenderers()
    {
        // Use UnsafeAccessor to access the section renderers dictionary from base class
        ref var sectionRenderers = ref GetSectionRenderers(this);
        
        if (sectionRenderers != null)
        {
            // Ensure all renderers have IsInMoreTab = false since we're preventing the More controller
            foreach (var renderer in sectionRenderers.Values)
            {
                renderer.IsInMoreTab = false;
            }
        }
    }
    
    protected override void OnShellItemSet(ShellItem shellItem)
    {
        base.OnShellItemSet(shellItem);
        
        // Ensure no renderers are marked as being in More tab
        EnsureNoMoreTabRenderers();
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

            // Show native tab bar items (in case they were hidden)
            HideNativeTabBarItems(false);
        }
        else
        {
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

            HideNativeTabBarItems(true);
        }
    }

    private void HideNativeTabBarItems(bool hidden)
    {
        // Show all subviews that are not our custom MAUI bar
        foreach (var subview in TabBar.Subviews)
        {
            if (subview.Tag != _mauiTabBarTag)
            {
                subview.Hidden = hidden;
            }
        }
    }
}
