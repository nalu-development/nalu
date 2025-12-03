using CoreGraphics;
using UIKit;

namespace Nalu;

#pragma warning disable CS1591
internal class NaluTabBarContainerView : UIView
{
    private readonly UIView _tabBar;

    public NaluTabBarContainerView(UIView tabBar)
    {
        _tabBar = tabBar;
        BackgroundColor = UIColor.Clear;
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
    }
}
