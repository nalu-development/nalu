using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Nalu.Maui.UITests;

public sealed class AppiumFixture : IDisposable
{
    private readonly AppiumDriver? _driver;

    public AppiumDriver App =>
        _driver ?? throw new NullReferenceException("AppiumDriver is null");

    public AppiumFixture()
    {
        // One-time setup
        AppiumServerHelper.StartAppiumLocalServer();

        var options = new AppiumOptions
                      {
                          AutomationName = "windows",
                          PlatformName = "Windows",
                          App = "com.companyname.basicappiumsample_9zz4h110yvjzm!App",
                      };

        _driver = new WindowsDriver(options);
    }

    public void Dispose()
    {
        // One-time teardown
        _driver?.Quit();
        AppiumServerHelper.DisposeAppiumLocalServer();
    }
}
