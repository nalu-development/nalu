using Plugin.Maui.UITestHelpers.Core;

namespace UITests;

// ReSharper disable once PartialTypeWithSinglePart
public partial class AppiumSetup
{
    private static IApp? _app;

    public static IApp App => _app ?? throw new NullReferenceException("App is null");
}
