using System.Collections.Specialized;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

internal class NaluShellItemRenderer : ShellItemRenderer
{
    private const nint _mauiTabBarTag = 0x63D2AF;
    private readonly IShellContext _shellContext;
    private UIView? _platformTabBar;
    private View? _crossPlatformTabBar;

    public NaluShellItemRenderer(IShellContext shellContext)
        : base(shellContext)
    {
        _shellContext = shellContext;
    }

    protected override void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsCollectionChanged(sender, e);
        UpdateTabBarView();
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
        else if (_crossPlatformTabBar != tabBarView)
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
