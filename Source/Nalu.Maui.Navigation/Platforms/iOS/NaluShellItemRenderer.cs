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
    
    // Store all view controllers here to prevent More controller
    private UIViewController[]? _allViewControllers;
    private bool _isManagingViewControllers;
    
    // Track last access time for LRU eviction
    private readonly Dictionary<UIViewController, long> _viewControllerAccessTimes = [];
    private long _accessCounter;

    public NaluShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
        _shellContext = shellContext;
    }
    
    // UnsafeAccessor methods to access private/internal members of base ShellItemRenderer
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_sectionRenderers")]
    private static extern ref Dictionary<UIViewController, IShellSectionRenderer>? GetSectionRenderers(ShellItemRenderer instance);
    
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<CurrentRenderer>k__BackingField")]
    private static extern ref IShellSectionRenderer? GetCurrentRendererField(ShellItemRenderer instance);
    
    public override UIViewController[]? ViewControllers
    {
        get => base.ViewControllers;
        set
        {
            if (_isManagingViewControllers)
            {
                // Already managing, just update base
                base.ViewControllers = value;
                return;
            }
            
            // Store all view controllers
            _allViewControllers = value;
            
            if (_allViewControllers is { Length: > _maxViewControllersToAvoidMore })
            {
                // Only pass the first few to prevent MoreViewController from ever showing
                _isManagingViewControllers = true;
                var limitedControllers = new UIViewController[_maxViewControllersToAvoidMore];
                Array.Copy(_allViewControllers, limitedControllers, _maxViewControllersToAvoidMore);
                
                // Initialize access times for the initially visible controllers
                for (var i = 0; i < limitedControllers.Length; i++)
                {
                    _viewControllerAccessTimes[limitedControllers[i]] = i == 0 ? 1 : 0;
                }
                
                base.ViewControllers = limitedControllers;
                _isManagingViewControllers = false;
            }
            else
            {
                // Not enough controllers to trigger More, use normal behavior
                base.ViewControllers = value;
            }
        }
    }
    
    public override UIViewController? SelectedViewController
    {
        get => base.SelectedViewController;
        set
        {
            // Track access time for this view controller (for LRU eviction)
            if (value != null)
            {
                _viewControllerAccessTimes[value] = ++_accessCounter;
            }
            
            // If we're managing view controllers and this is a selection beyond our limited set
            if (_allViewControllers is { Length: > _maxViewControllersToAvoidMore } &&
                ViewControllers is { } currentTabs &&
                ViewControllers?.Contains(value) is not true &&
                value is not null)
            {
                var currentSelection = base.SelectedViewController;
                
                // Find the least recently used view controller (LRU) that isn't currently selected
                var lruIndex = FindLeastRecentlyUsedIndex(currentTabs, currentSelection);
                    
                // This controller is beyond the limited set shown to UITabBarController
                // Swap it into the visible ViewControllers array so UITabBarController can select it
                var copiedViewControllers = new UIViewController[_maxViewControllersToAvoidMore];
                Array.Copy(currentTabs, copiedViewControllers, _maxViewControllersToAvoidMore);
                copiedViewControllers[lruIndex] = value;
                base.ViewControllers = copiedViewControllers;
                UpdateTabBarView();
            }
            
            // Normal selection for controllers in the visible set
            base.SelectedViewController = value;
        }
    }
    
    private int FindLeastRecentlyUsedIndex(UIViewController[] currentTabs, UIViewController? currentSelection)
    {
        var lruIndex = 0;
        var oldestAccessTime = long.MaxValue;
        
        for (var i = 0; i < currentTabs.Length; i++)
        {
            var controller = currentTabs[i];
            
            // Skip the currently selected controller
            if (controller == currentSelection)
            {
                continue;
            }
            
            // Get access time (default to 0 if never accessed)
            var accessTime = _viewControllerAccessTimes.TryGetValue(controller, out var time) ? time : 0;
            
            // Find the one with the oldest (smallest) access time
            if (accessTime < oldestAccessTime)
            {
                oldestAccessTime = accessTime;
                lruIndex = i;
            }
        }
        
        return lruIndex;
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
