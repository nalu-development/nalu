// using CoreAnimation;
using CoreGraphics;
using UIKit;

namespace Nalu;

#pragma warning disable CS1591
internal class NaluTabBarContainerView : UIView
{
    private readonly UIView _tabBar;
    // private readonly UIVisualEffectView? _blurView;

    public NaluTabBarContainerView(UIView tabBar)
    {
        _tabBar = tabBar;
        BackgroundColor = UIColor.Clear;

        // if (OperatingSystem.IsIOSVersionAtLeast(13))
        // {
        //     var blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemThinMaterial);
        //     _blurView = new UIVisualEffectView(blurEffect)
        //                 {
        //                     AutoresizingMask = UIViewAutoresizing.FlexibleDimensions
        //                 };
        //     
        //     var verticalGradient = new CAGradientLayer
        //     {
        //         Colors =
        //         [
        //             UIColor.Clear.CGColor,
        //             UIColor.White.CGColor
        //         ],
        //         StartPoint = new CGPoint(0.5, 0),
        //         EndPoint = new CGPoint(0.5, 0.15),
        //     };
        //     
        //     _blurView.Layer.Mask = verticalGradient;
        //     
        //     AddSubview(_blurView);
        // }

        AddSubview(_tabBar);
    }

    public bool NeedsMeasure { get; private set; } = true;

    public override CGSize SizeThatFits(CGSize size)
    {
        NeedsMeasure = false;
        return _tabBar.SizeThatFits(size);
    }

    public override void SafeAreaInsetsDidChange()
    {
        base.SafeAreaInsetsDidChange();
        NeedsMeasure = true;
    }

    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();
        NeedsMeasure = true;
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _tabBar.Frame = Bounds;

        // if (_blurView != null)
        // {
        //     _blurView.Frame = Bounds;
        //     _blurView.Layer.Mask!.Frame = _blurView.Bounds;
        // }
    }
}
