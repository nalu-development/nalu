using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// The container view used to display the tab bar.
/// </summary>
/// <remarks>
/// You can leverage the static properties of this class to enable and customize a blur effect for the tab bar background.
/// </remarks>
public class NaluTabBarContainerView : UIView
{
    private readonly UIView _tabBar;
    private readonly UIVisualEffectView? _blurView;

    /// <summary>
    /// Creates a new instance of <see cref="NaluTabBarContainerView"/>.
    /// </summary>
    /// <param name="tabBar"></param>
    public NaluTabBarContainerView(UIView tabBar)
    {
        _tabBar = tabBar;
        BackgroundColor = UIColor.Clear;

        if (NaluTabBar.UseBlurEffect)
        {
            var blurEffect = NaluTabBar.BlurEffectFactory();
            _blurView = new UIVisualEffectView(blurEffect)
                        {
                            AutoresizingMask = UIViewAutoresizing.FlexibleDimensions
                        };
            
            _blurView.Layer.Mask = NaluTabBar.BlurMaskFactory();
            
            AddSubview(_blurView);
        }

        AddSubview(_tabBar);
    }

    internal bool NeedsMeasure { get; private set; } = true;

    /// <inheritdoc/>
    public override CGSize SizeThatFits(CGSize size)
    {
        NeedsMeasure = false;
        return _tabBar.SizeThatFits(size);
    }

    /// <inheritdoc/>
    public override void SafeAreaInsetsDidChange()
    {
        base.SafeAreaInsetsDidChange();
        NeedsMeasure = true;
    }

    /// <inheritdoc/>
    public override void SetNeedsLayout()
    {
        base.SetNeedsLayout();
        NeedsMeasure = true;
    }

    /// <inheritdoc/>
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _tabBar.Frame = Bounds;

        if (_blurView != null)
        {
            _blurView.Frame = Bounds;
            _blurView.Layer.Mask!.Frame = _blurView.Bounds;
        }
    }
}
