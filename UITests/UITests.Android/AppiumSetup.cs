using OpenQA.Selenium.Appium.Enums;
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
        
        var config = new Config();
        config.SetProperty("PlatformName", "Android");
        config.SetProperty("AppId", "com.nalu.maui.testapp");
        config.SetProperty("DeviceName", "Pixel 8a API 35");

		// DEBUG BUILD SETUP
        // If you're running your tests against debug builds you'll need to set NoReset to true
        // otherwise appium will delete all the libraries used for Fast Deployment on Android
        // Release builds have Fast Deployment disabled
        // https://learn.microsoft.com/xamarin/android/deploy-test/building-apps/build-process#fast-deployment
        config.SetProperty(MobileCapabilityType.NoReset, "true");
        config.SetProperty(AndroidMobileCapabilityType.AppPackage, "com.nalu.maui.testapp");
        
        // RELEASE BUILD SETUP
        // The full path to the .apk file
        // This only works with release builds because debug builds have fast deployment enabled
        // and Appium isn't compatible with fast deployment
        // App = Path.Join(TestContext.CurrentContext.TestDirectory, "../../../../MauiApp/bin/Release/net9.0-android/com.nalu.maui.testapp-Signed.apk"),
        // END RELEASE BUILD SETUP

        //Make sure to set [Register("com.nalu.maui.testapp")] on the MainActivity of your android application
		config.SetProperty(AndroidMobileCapabilityType.AppActivity, $"com.nalu.maui.testapp.MainActivity");
        // END DEBUG BUILD SETUP


        // Specifying the avd option will boot the emulator for you
        // make sure there is an emulator with the name below
        // If not specified, make sure you have an emulator booted
        //androidOptions.AddAdditionalAppiumOption("avd", "pixel_5_-_api_33");

        // Note there are many more options that you can use to influence the app under test according to your needs
        _app = AppiumAndroidApp.CreateAndroidApp(AppiumServerHelper.ServiceUri, config);
	}

	public void Dispose()
	{
        _app?.Dispose();
		// If an Appium server was started locally above, make sure we clean it up here
		AppiumServerHelper.DisposeAppiumLocalServer();
	}
}
