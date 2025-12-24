using Microsoft.Extensions.DependencyInjection;
using OpenQA.Selenium.Appium;
using AndroidSdk;
using OpenQA.Selenium.Appium.Android;

namespace Nalu.Maui.UITests;

public class Startup
{
    // This is called automatically by the runner
    public void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<IAppiumAppProvider, AppiumApp>();
}

file class AppiumApp : IAppiumApp, IAppiumAppProvider, IDisposable
{
    public const string Platform = "Android";
    public const string AvdName = "Pixel 8a API 35";
    public const string PackageName = "com.nalu.maui.testapp";

    private readonly ITestOutputHelper _testOutputHelper;
    private AppiumServiceHelper? _appiumService;
    private Emulator.AndroidEmulatorProcess? _emulatorProcess;
    private AppiumDriver? _driver;

    public string DeviceName => AvdName;
    public TestDevice TestDevice => TestDevice.Android;

    public AppiumDriver Driver => _driver ?? throw new InvalidOperationException("Driver not initialized. Call GetAsync() first.");

    public AppiumApp(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public void Dispose()
    {
        Driver.Quit();
        _emulatorProcess?.Shutdown();
        _appiumService?.Dispose();
    }

    private static async Task<AndroidSdkManager> InstallSoftware()
    {
        const string avdSdkId = "system-images;android-35;google_apis_playstore;arm64";

        var sdkPackages = new[]
                          {
                              "platforms;android-35"
                          };

        var sdk = new AndroidSdkManager();
        await sdk.Acquire();
        sdk.SdkManager.Install(sdkPackages);
        sdk.SdkManager.Install(avdSdkId);
        if (sdk.AvdManager.ListAvds().All(x => x.Name != AvdName))
        {
            sdk.AvdManager.Create(AvdName, avdSdkId, "pixel", force: true);
        }

        return sdk;
    }

    private string GetApp()
    {
#if DEBUG
        const string configuration = "Debug";
#else
		const string configuration = "Release";
#endif
        const string testsPath = $@"Client.Android.UITests\bin\{configuration}\net10.0";
        var solutionPath = Environment.CurrentDirectory.Replace(testsPath, string.Empty);
        var path = $@"{solutionPath}Client\bin\{configuration}\net10.0-android\{PackageName}-Signed.apk";
        _testOutputHelper.WriteLine(path);
        return path;
    }

    public async Task<IAppiumApp> GetAsync()
    {
        if (_driver != null)
        {
            return this;
        }
        
        var sdk = await InstallSoftware();
        _emulatorProcess = sdk.Emulator.Start(AvdName, new Emulator.EmulatorStartOptions { NoSnapshot = true });
        _emulatorProcess.WaitForBootComplete();

        _appiumService = new AppiumServiceHelper();
        _appiumService.StartAppiumLocalServer();

        var options = new AppiumOptions
                      {
                          AutomationName = "UIAutomator2",
                          PlatformName = Platform,
                          PlatformVersion = "13",
                          App = GetApp()
                      };

        _driver = new AndroidDriver(options);
        
        return this;
    }
}
