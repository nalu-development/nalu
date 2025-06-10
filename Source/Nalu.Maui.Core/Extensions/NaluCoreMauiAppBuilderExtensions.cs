namespace Nalu;

/// <summary>
/// Extensions for <see cref="MauiAppBuilder"/> to configure Nalu for Maui applications.
/// </summary>
public static class NaluCoreMauiAppBuilderExtensions
{
    /// <summary>
    /// Uses the Nalu soft keyboard manager instead of the built-in one.
    /// </summary>
    public static MauiAppBuilder UseNaluSoftKeyboardManager(this MauiAppBuilder builder, SoftKeyboardAdjustMode defaultAdjustMode = SoftKeyboardAdjustMode.Resize)
    {
        SoftKeyboardManager.DefaultAdjustMode = defaultAdjustMode;

#if IOS || ANDROID
        SoftKeyboardManager.Configure(builder);
#endif

        return builder;
    }
}
