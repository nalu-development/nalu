using Android.Content;
using Android.Graphics;
using Microsoft.Maui.Platform;
using AColor = Android.Graphics.Color;

namespace Nalu;

public partial class NaluTabBar
{
    /// <summary>
    /// Gets or sets the default blur radius used by the default <see cref="BlurEffectFactory"/>.
    /// </summary>
    public static double DefaultBlurRadius { get; set; } = 8;
    
    /// <summary>
    /// Gets or sets the factory method to create the blur effect.
    /// </summary>
    /// <remarks>
    /// This will only be invoked on Android 12 (API level 31) and above.
    /// </remarks>
    public static Func<Context, RenderEffect> BlurEffectFactory { get; set; } = context =>
    {
        var pixels = context.ToPixels(DefaultBlurRadius);
#pragma warning disable CA1416
        return RenderEffect.CreateBlurEffect(
            pixels,
            pixels,
            Shader.TileMode.Clamp!
        );
#pragma warning restore CA1416
    };
    
    /// <summary>
    /// Gets or sets the factory method to create the mask layer for the blur effect.
    /// </summary>
    // ReSharper disable once UnusedParameter.Local
    public static Func<int, int, Shader?> BlurShaderFactory { get; set; } = (width, height) => new LinearGradient(
        0,
        0,
        0,
        height,
        [AColor.Transparent.ToArgb(), AColor.White.ToArgb()],
        [0f, 0.2f],
        Shader.TileMode.Clamp!
    );
}
