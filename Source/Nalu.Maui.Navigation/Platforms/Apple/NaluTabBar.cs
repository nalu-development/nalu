using CoreAnimation;
using CoreGraphics;
using UIKit;

namespace Nalu;

public partial class NaluTabBar
{
    /// <summary>
    /// Gets or sets the style of the blur effect used by the default <see cref="BlurEffectFactory"/>.
    /// </summary>
    public static UIBlurEffectStyle DefaultBlurStyle { get; set; }
#if IOS
        = OperatingSystem.IsIOSVersionAtLeast(13) ? UIBlurEffectStyle.SystemThinMaterial : UIBlurEffectStyle.Regular;
#else
        = UIBlurEffectStyle.SystemThinMaterial;
#endif

    /// <summary>
    /// Gets or sets the factory method to create the blur effect.
    /// </summary>
    public static Func<UIBlurEffect> BlurEffectFactory { get; set; } = () => UIBlurEffect.FromStyle(DefaultBlurStyle);
    
    /// <summary>
    /// Gets or sets the factory method to create the mask layer for the blur effect.
    /// </summary>
    public static Func<CALayer?> BlurMaskFactory { get; set; } = () => new CAGradientLayer
                                                                       {
                                                                           Colors =
                                                                           [
                                                                               UIColor.Clear.CGColor,
                                                                               UIColor.White.CGColor
                                                                           ],
                                                                           StartPoint = new CGPoint(0.5, 0),
                                                                           EndPoint = new CGPoint(0.5, 0.2),
                                                                       };
}
