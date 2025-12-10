using System.ComponentModel;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.View;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using View = Microsoft.Maui.Controls.View;

// ReSharper disable VirtualMemberCallInConstructor

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

public class NaluShellItemRenderer : ShellItemRendererBase
{
    public static readonly bool SupportsEdgeToEdge
#if NET10_0_OR_GREATER
        = true;
#else
        = false;
#endif
    
    private NaluShellItemRendererOuterLayout? _outerLayout;
    private NaluShellItemRendererNavigationLayout? _navigationLayout;
    private NaluShellItemRendererTabBarLayout? _tabBarLayout;
    private FrameLayout? _navigationTarget;
    private AView? _tabBar;
    private View? _crossPlatformTabBar;
    private AView? _tabBarScrim;
    private View? _crossPlatformTabBarScrim;
    private IMauiContext MauiContext => ShellContext.Shell.Handler?.MauiContext ?? throw new InvalidOperationException("MauiContext is not available.");

    public NaluShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
    }

    public override AView? OnCreateView(LayoutInflater inflater, AViewGroup? container, Bundle? savedInstanceState)
    {
        base.OnCreateView(inflater, container, savedInstanceState);
        
        var context = MauiContext.Context ?? throw new InvalidOperationException("Context is not available.");
        _outerLayout = new NaluShellItemRendererOuterLayout(context);
        _tabBarLayout = new NaluShellItemRendererTabBarLayout(context);
        _navigationLayout = SupportsEdgeToEdge && NaluTabBar.UseBlurEffect && NaluShellItemRendererNavigationBlurSupportLayout.IsSupported
            ? new NaluShellItemRendererNavigationBlurSupportLayout(context)
            : new NaluShellItemRendererNavigationLayout(context);
        _navigationTarget = new FrameLayout(context);
        
        // generate IDs
        var navigationTargetId = AView.GenerateViewId();
        var navigationLayoutId = AView.GenerateViewId();
        var tabBarLayoutId = AView.GenerateViewId();
        var outerLayoutId = AView.GenerateViewId();
        _navigationTarget.Id = navigationTargetId;
        _navigationLayout.Id = navigationLayoutId;
        _tabBarLayout.Id = tabBarLayoutId;
        _outerLayout.Id = outerLayoutId;
        
        // setup navigation target to fill the navigation layout
        _navigationTarget.LayoutParameters = new FrameLayout.LayoutParams(
            AViewGroup.LayoutParams.MatchParent,
            AViewGroup.LayoutParams.MatchParent
        );
        _navigationLayout.AddView(_navigationTarget);
        
        // constrain navigation layout to fill the parent
        var navigationLayoutParams = new ConstraintLayout.LayoutParams(0, 0);
        navigationLayoutParams.TopToTop = outerLayoutId;

        if (SupportsEdgeToEdge)
        {
            navigationLayoutParams.BottomToBottom = outerLayoutId;
        }
        else
        {
            _outerLayout.SetFitsSystemWindows(true);
            navigationLayoutParams.BottomToTop = tabBarLayoutId;
        }

        navigationLayoutParams.StartToStart = outerLayoutId;
        navigationLayoutParams.EndToEnd = outerLayoutId;
        _navigationLayout.LayoutParameters = navigationLayoutParams;

        _outerLayout.AddView(_navigationLayout);
        
        // constrain tab bar layout to the bottom
        var tabBarLayoutParams = new ConstraintLayout.LayoutParams(0, AViewGroup.LayoutParams.WrapContent);
        tabBarLayoutParams.BottomToBottom = outerLayoutId;
        tabBarLayoutParams.StartToStart = outerLayoutId;
        tabBarLayoutParams.EndToEnd = outerLayoutId;
        _tabBarLayout.LayoutParameters = tabBarLayoutParams;
        
        _outerLayout.AddView(_tabBarLayout);

        // Setup outer layout to fill the parent
        _outerLayout.LayoutParameters = new FrameLayout.LayoutParams(
            AViewGroup.LayoutParams.MatchParent,
            AViewGroup.LayoutParams.MatchParent
        );

        UpdateTabBarView();
        UpdateTabBarScrimView();
        
        HookEvents(ShellItem);

        return _outerLayout;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        _tabBarLayout?.SetTabBar(null);

        _tabBar = null;
        _crossPlatformTabBar?.DisconnectHandlers();
        _crossPlatformTabBar = null;
        
        _tabBarScrim?.RemoveFromParent();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        _navigationTarget?.Dispose();
        _outerLayout?.Dispose();
        _navigationLayout?.Dispose();
        _tabBarLayout?.Dispose();

        _outerLayout = null;
        _navigationLayout = null;
        _tabBarLayout = null;
        
        UnhookEvents(ShellItem);
    }

    protected override AViewGroup GetNavigationTarget() => _navigationTarget ?? throw new InvalidOperationException("NavigationTarget has not been created yet.");

    protected override void OnShellItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        base.OnShellItemPropertyChanged(sender, e);

        if (e.PropertyName == NaluShell.TabBarViewProperty.PropertyName)
        {
            UpdateTabBarView();
        }
        else if (e.PropertyName == NaluShell.TabBarScrimViewProperty.PropertyName)
        {
            UpdateTabBarScrimView();
        }
    }

    private void UpdateTabBarView()
    {
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        _tabBar?.RemoveFromParent();
        _tabBar = null;
        _crossPlatformTabBar?.DisconnectHandlers();
        _crossPlatformTabBar = null;
        
        _tabBarScrim?.RemoveFromParent();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        if (tabBarView == null)
        {
            _tabBarLayout?.SetTabBar(null);
        }
        else
        {
            var mauiContext = ShellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarView.ToPlatform(mauiContext);
            _tabBar = platformView;
            _tabBarLayout?.SetTabBar(platformView);
            _crossPlatformTabBar = tabBarView;
        }

        UpdateTabBarHidden();
    }
    
    private void UpdateTabBarScrimView()
    {
        var tabBarScrimView = NaluShell.GetTabBarScrimView(ShellItem);

        _tabBarScrim?.RemoveFromParent();
        _tabBarScrim = null;
        _crossPlatformTabBarScrim?.DisconnectHandlers();
        _crossPlatformTabBarScrim = null;

        if (tabBarScrimView != null)
        {
            var mauiContext = ShellContext.Shell.Handler?.MauiContext ?? throw new NullReferenceException("MauiContext is null");
            var platformView = tabBarScrimView.ToPlatform(mauiContext);
            _tabBarScrim = platformView;
            _crossPlatformTabBarScrim = tabBarScrimView;
            _navigationLayout!.AddView(_tabBarScrim);
        }
    }

    private void UpdateTabBarHidden()
    {
        var isTabBarVisible = _tabBar is not null && (DisplayedPage?.GetValue(Shell.TabBarIsVisibleProperty) as bool? ?? true);

        if (_tabBarLayout is not null)
        {
            _tabBarLayout.Visibility = isTabBarVisible ? ViewStates.Visible : ViewStates.Gone;
            ViewCompat.RequestApplyInsets(_outerLayout);
        }
    }

    protected override void OnDisplayedPageChanged(Page? newPage, Page? oldPage)
    {
        base.OnDisplayedPageChanged(newPage, oldPage);

        if (oldPage is not null)
        {
            oldPage.PropertyChanged -= OnDisplayedElementPropertyChanged;
        }

        if (newPage is not null)
        {
            newPage.PropertyChanged += OnDisplayedElementPropertyChanged;
        }

        UpdateTabBarVisibility();
    }

    private void UpdateTabBarVisibility()
    {
        if (_tabBarLayout is null)
        {
            return;
        }

        var isTabBarVisible = DisplayedPage?.GetValue(Shell.TabBarIsVisibleProperty) as bool? ?? true;
        _tabBarLayout.Visibility = isTabBarVisible ? ViewStates.Visible : ViewStates.Gone;
    }

    private void OnDisplayedElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == Shell.TabBarIsVisibleProperty.PropertyName)
        {
            UpdateTabBarVisibility();
        }
    }
}
