using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

internal class NaluShellRenderer : ShellRenderer
{
    protected override IShellTabBarAppearanceTracker CreateTabBarAppearanceTracker()
    {
        var platformViewHandler = (IPlatformViewHandler) this;

        return new NaluTabBarAppearanceTracker(platformViewHandler.MauiContext ?? throw new NullReferenceException("MauiContext is null"));
    }
}

internal class NaluTabBarAppearanceTracker : ShellTabBarAppearanceTracker
{
    private const nint _mauiTabBarTag = 0x63D2AA;
    private readonly IMauiContext _mauiContext;
    private UIView? _nativeMauiBar;
    private UITabBarController? _controller;

    public NaluTabBarAppearanceTracker(IMauiContext mauiContext)
    {
        _mauiContext = mauiContext;
    }

    public override void SetAppearance(UITabBarController controller, ShellAppearance appearance)
    {
        base.SetAppearance(controller, appearance);
        _controller = controller;

        // Create & add MAUI bar only once
        if (_nativeMauiBar == null)
        {
            var mauiBar = new Grid
                          {
                              BackgroundColor = Colors.Red
                          };

            var platformView = mauiBar.ToPlatform(_mauiContext);
            platformView.Tag = _mauiTabBarTag;
            _nativeMauiBar = platformView;
        }

        var tabBarContainer = controller.TabBar;

        if (tabBarContainer.Subviews.All(v => v.Tag != _mauiTabBarTag))
        {
            tabBarContainer.AddSubview(_nativeMauiBar!);
            _nativeMauiBar!.TranslatesAutoresizingMaskIntoConstraints = false;

            NSLayoutConstraint.ActivateConstraints(
                [
                    _nativeMauiBar.LeadingAnchor.ConstraintEqualTo(tabBarContainer.LeadingAnchor),
                    _nativeMauiBar.TrailingAnchor.ConstraintEqualTo(tabBarContainer.TrailingAnchor),
                    _nativeMauiBar.BottomAnchor.ConstraintEqualTo(tabBarContainer.SafeAreaLayoutGuide.BottomAnchor),
                    _nativeMauiBar.TopAnchor.ConstraintEqualTo(tabBarContainer.TopAnchor)
                ]
            );
        }

        // // sync visibility and insets
        // UpdateVisibilityAndInsets(false);
        //
        // // for push/pop animations
        // controller.GetTransitionCoordinator()?.AnimateAlongsideTransition(
        //     _ => UpdateVisibilityAndInsets(animated: true),
        //     null
        // );
    }

    // public override void ResetAppearance(UITabBarController controller)
    // {
    //     base.ResetAppearance(controller);
    //     UpdateVisibilityAndInsets(false);
    // }
    //
    // private void UpdateVisibilityAndInsets(bool animated)
    // {
    //     var shell = (AppShell) Application.Current!.MainPage!;
    //     var current = shell.CurrentItem?.CurrentItem?.CurrentItem;
    //
    //     var hide = current?.Content is Element e
    //                && HideableShellTabBar.GetHideShellTabBar(e);
    //
    //     // native bar always hidden
    //     _controller.TabBar.Hidden = true;
    //     // toggle our MAUI bar
    //     _nativeMauiBar!.Hidden = hide;
    //
    //     // inset content above bar when visible
    //     var inset = hide ? 0f : 60f;
    //
    //     if (animated)
    //     {
    //         _controller.TopViewController?.AdditionalSafeAreaInsets =
    //             new UIEdgeInsets(0, 0, inset, 0);
    //     }
    //     else
    //     {
    //         _controller.TopViewController?.AdditionalSafeAreaInsets =
    //             new UIEdgeInsets(0, 0, inset, 0);
    //     }
    // }
}
