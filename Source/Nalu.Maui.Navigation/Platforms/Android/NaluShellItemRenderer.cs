using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Android.OS;
using Android.Views;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using View = Android.Views.View;
using ViewGroup = Android.Views.ViewGroup;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Nalu;

public class NaluShellItemRenderer : ShellItemRenderer
{
    private View? _platformTabBar;
    private Microsoft.Maui.Controls.View? _crossPlatformTabBar;

    public NaluShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bottomView")]
    private static extern ref BottomNavigationView GetBottomView(ShellItemRenderer instance);

    protected override void OnShellItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        base.OnShellItemsChanged(sender, e);
        UpdateTabBarView();
    }

    public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
    {
        var view = base.OnCreateView(inflater, container, savedInstanceState);
        UpdateTabBarView();

        return view;
    }

    public void UpdateTabBarView()
    {
        // Get the TabBarView from the ShellItem
        var tabBarView = NaluShell.GetTabBarView(ShellItem);

        if (tabBarView == null)
        {
            // Remove custom view if it was previously added
            _crossPlatformTabBar?.DisconnectHandlers();
            _platformTabBar?.RemoveFromParent();
            _platformTabBar = null;
            _crossPlatformTabBar = null;

            // Show native tab bar items (in case they were hidden)
            HideNativeMenuItems(false);
        }
        else if (_crossPlatformTabBar != tabBarView)
        {
            _crossPlatformTabBar?.DisconnectHandlers();
            _platformTabBar?.RemoveFromParent();

            var mauiContext = ShellContext.Shell.Handler?.MauiContext ?? throw new InvalidOperationException("MauiContext is null");
            var platformView = tabBarView.ToPlatform(mauiContext);
            platformView.TranslationZ = 1;
            platformView.Clickable = true;
            platformView.Focusable = true;

            // Set layout parameters to fill the entire BottomNavigationView
            var layoutParams = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent
            );
            platformView.LayoutParameters = layoutParams;

            _platformTabBar = platformView;
            _crossPlatformTabBar = tabBarView;
            var bottomView = GetBottomView(this);
            bottomView.AddView(_platformTabBar);
            HideNativeMenuItems(true);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        _crossPlatformTabBar?.DisconnectHandlers();
        _platformTabBar?.RemoveFromParent();
        _crossPlatformTabBar = null;
        _platformTabBar = null;
    }

    private void HideNativeMenuItems(bool hidden)
    {
        // Hide/show the BottomNavigationMenuView which contains the native menu items
        var bottomView = GetBottomView(this);
#pragma warning disable XAOBS001
        if (bottomView.GetChildAt(0) is BottomNavigationMenuView bottomNavMenuView)
        {
            bottomNavMenuView.Visibility = hidden ? ViewStates.Gone : ViewStates.Visible;
        }
#pragma warning restore XAOBS001
    }
}
