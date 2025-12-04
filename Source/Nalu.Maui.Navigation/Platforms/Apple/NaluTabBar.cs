using CoreAnimation;
using UIKit;

namespace Nalu;

public partial class NaluTabBar
{
    /// <summary>
    /// Gets or sets the style of the blur effect used by the default <see cref="BlurEffectFactory"/>.
    /// </summary>
    public static UIBlurEffectStyle DefaultBlurStyle { get; set; }

#pragma warning disable CS1574, CS1584, CS1581, CS1580
    /// <summary>
    /// Gets or sets the factory method to create the blur/glass effect.
    /// </summary>
    /// <remarks>
    /// On iOS 26 and above, a <see cref="UIGlassEffect"/> will be created instead of a <see cref="UIBlurEffect"/>.
    /// </remarks>
#pragma warning restore CS1574, CS1584, CS1581, CS1580
    public static Func<UIVisualEffect> BlurEffectFactory { get; set; } = () =>
    {
#if IOS26_0_OR_GREATER || MACCATALYST26_0_OR_GREATER
        if (OperatingSystem.IsIOSVersionAtLeast(26) || OperatingSystem.IsMacCatalystVersionAtLeast(26))
        {
            return new UIGlassEffect();
        }
#endif

        return UIBlurEffect.FromStyle(UIBlurEffectStyle.Light);
    };
    
    /// <summary>
    /// Gets or sets the factory method to create the mask layer for the blur effect.
    /// </summary>
    /// <remarks>
    /// Effects work properly only with a fully opaque layer.
    /// Using this may lead to unexpected results, so use with caution.
    /// </remarks>
    public static Func<CALayer?> BlurMaskFactory { get; set; } = () => null;
}
