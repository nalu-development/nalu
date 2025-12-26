using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;
using UITests;
using Xunit;

[assembly:AssemblyFixture(typeof(AppiumSetup))]

namespace UITests;

public partial class AppiumSetup : IDisposable
{
	public AppiumSetup()
	{
		// If you started an Appium server manually, make sure to comment out the next line
		// This line starts a local Appium server for you as part of the test run
		AppiumServerHelper.StartAppiumLocalServer();

		// Note there are many more options that you can use to influence the app under test according to your needs
        var config = new Config();
        config.SetProperty("AppId", "com.nalu.maui.testapp");
        config.SetProperty("PlatformName", "iOS");
        config.SetProperty("DeviceName", "iPhone Xs");
        config.SetProperty("PlatformVersion", "18.5");
        _app = new AppiumIOSApp(AppiumServerHelper.ServiceUri, config);
	}

	public void Dispose()
	{
		_app?.Dispose();
		// If an Appium server was started locally above, make sure we clean it up here
		AppiumServerHelper.DisposeAppiumLocalServer();
	}
}
