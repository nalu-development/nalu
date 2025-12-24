using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ImageMagick;
using ImageMagick.Drawing;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using VisualTestUtils;
using VisualTestUtils.MagickNet;
using Xunit;

namespace Nalu.Maui.UITests;

public abstract class BaseTest(ITestOutputHelper testOutputHelper, IAppiumAppProvider appProvider) : IAsyncLifetime
{
    // ReSharper disable once InconsistentNaming
    private const string _iOSDefaultVersion = "18.5";
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    private const double _differenceThreshold = 1 / 100d; // 1% difference

    private readonly VisualRegressionTester _visualRegressionTester = new(
        testRootDirectory: Environment.CurrentDirectory,
        visualComparer: new MagickNetVisualComparer(differenceThreshold: _differenceThreshold),
        visualDiffGenerator: new MagickNetVisualDiffGenerator(),
        ciArtifactsDirectory: Environment.GetEnvironmentVariable("Build.ArtifactStagingDirectory")
    );

    private readonly MagickNetImageEditorFactory _imageEditorFactory = new();
    private IAppiumApp? _app;

    protected AppiumDriver App => _app?.Driver ?? throw new InvalidOperationException("App not initialized. Ensure dependency injection is configured correctly.");
    private string DeviceName => _app?.DeviceName ?? throw new InvalidOperationException("App not initialized. Ensure dependency injection is configured correctly.");
    private TestDevice TestDevice => _app?.TestDevice ?? throw new InvalidOperationException("App not initialized. Ensure dependency injection is configured correctly.");

    protected AppiumElement FindUiElement(string id) => App.FindElement(App is WindowsDriver ? MobileBy.AccessibilityId(id) : MobileBy.Id(id));

    public async ValueTask InitializeAsync()
    {
        _app = await appProvider.GetAsync();
        testOutputHelper.WriteLine($"CIArtifactsDirectory: {Environment.GetEnvironmentVariable("Build.ArtifactStagingDirectory")}");
    }

    public ValueTask DisposeAsync()
    {
        App.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Verifies the screenshots and returns an exception in case of failure.
    /// </summary>
    /// <remarks>
    /// This is especially useful when capturing multiple screenshots in a single UI test.
    /// </remarks>
    /// <example>
    /// <code>
    /// Exception? exception = null;
    /// VerifyScreenshotOrSetException(ref exception, "MyScreenshotName");
    /// VerifyScreenshotOrSetException(ref exception, "MyOtherScreenshotName");
    /// if (exception is not null) throw exception;
    /// </code>
    /// </example>
    public void VerifyScreenshotOrSetException(
        ref Exception? exception,
        string? name = null,
        TimeSpan? retryDelay = null,
        uint cropLeft = 0,
        uint cropRight = 0,
        uint cropTop = 0,
        uint cropBottom = 0,
        double tolerance = 0.0
#if MACUITEST || WINTEST
		, bool includeTitleBar = false
#endif
    )
    {
        try
        {
            VerifyScreenshot(
                name,
                retryDelay,
                cropLeft,
                cropRight,
                cropTop,
                cropBottom,
                tolerance
#if MACUITEST || WINTEST
			, includeTitleBar
#endif
            );
        }
        catch (Exception ex)
        {
            exception ??= ex;
        }
    }

    /// <summary>
    /// Verifies a screenshot by comparing it against a baseline image and throws an exception if verification fails.
    /// </summary>
    /// <param name="name">Optional name for the screenshot. If not provided, a default name will be used.</param>
    /// <param name="retryDelay">Optional delay between retry attempts when verification fails.</param>
    /// <param name="cropLeft">Number of pixels to crop from the left of the screenshot.</param>
    /// <param name="cropRight">Number of pixels to crop from the right of the screenshot.</param>
    /// <param name="cropTop">Number of pixels to crop from the top of the screenshot.</param>
    /// <param name="cropBottom">Number of pixels to crop from the bottom of the screenshot.</param>
    /// <param name="tolerance">Tolerance level for image comparison as a percentage from 0 to 100.</param>
    /// <param name="includeTitleBar">Whether to include the title bar in the screenshot comparison.</param>
    /// <remarks>
    /// This method immediately throws an exception if the screenshot verification fails.
    /// For batch verification of multiple screenshots, consider using <see cref="VerifyScreenshotOrSetException"/> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Exact match (no tolerance)
    /// VerifyScreenshot("LoginScreen");
    /// 
    /// // Allow 2% difference for dynamic content
    /// VerifyScreenshot("DashboardWithTimestamp", tolerance: 2.0);
    /// 
    /// // Allow 5% difference for animations or slight rendering variations
    /// VerifyScreenshot("ButtonHoverState", tolerance: 5.0);
    /// 
    /// // Combined with cropping and tolerance
    /// VerifyScreenshot("HeaderSection", cropTop: 50, cropBottom: 100, tolerance: 3.0);
    /// </code>
    /// </example>
    public void VerifyScreenshot(
        string? name = null,
        TimeSpan? retryDelay = null,
        uint cropLeft = 0,
        uint cropRight = 0,
        uint cropTop = 0,
        uint cropBottom = 0,
        double tolerance = 0.0, // Add tolerance parameter (0.05 = 5%)
        bool includeTitleBar = false
    )
    {
#if !(MACCATALYST || WINDOWS)
        if (includeTitleBar)
        {
            throw new NotSupportedException("Including the title bar is only supported on Mac Catalyst and Windows platforms.");
        }
#endif

        retryDelay ??= TimeSpan.FromMilliseconds(500);

        // Retry the verification once in case the app is in a transient state
        try
        {
            Verify(name);
        }
        catch
        {
            Thread.Sleep(retryDelay.Value);
            Verify(name);
        }

        void Verify(string? name)
        {
            var deviceName = DeviceName;

            /*
            Determine the environmentName, used as the directory name for visual testing snaphots. Here are the rules/conventions:
            - Names are lower case, no spaces.
            - By default, the name matches the platform (android, ios, windows, or mac).
            - Each platform has a default device (or set of devices) - if the snapshot matches the default no suffix is needed (e.g. just ios).
            - If tests are run on secondary devices that produce different snapshots, the device name is used as suffix (e.g. ios-iphonex).
            - If tests are run on secondary devices with multiple OS versions that produce different snapshots, both device name and os version are
            used as a suffix (e.g. ios-iphonex-16_4). We don't have any cases of this today but may eventually. The device name comes first here,
            before os version, because most visual testing differences come from different sceen size (a device thing), not OS version differences,
            but both can happen.
            */
            var environmentName = "";

            switch (TestDevice)
            {
                case TestDevice.Android:
                    environmentName = "android";

                    var deviceApiLevel = (long?) App.Capabilities.GetCapability("deviceApiLevel")
                                         ?? throw new InvalidOperationException("deviceApiLevel capability is missing or null.");

                    var deviceScreenSize = (string?) App.Capabilities.GetCapability("deviceScreenSize")
                                           ?? throw new InvalidOperationException("deviceScreenSize capability is missing or null.");

                    var deviceScreenDensity = (long?) App.Capabilities.GetCapability("deviceScreenDensity")
                                              ?? throw new InvalidOperationException("deviceScreenDensity capability is missing or null.");

                    if (deviceApiLevel == 36)
                    {
                        environmentName = "android-notch-36";
                    }

                    if (!((deviceApiLevel == 30 &&
                           (deviceScreenSize.Equals("1080x1920", StringComparison.OrdinalIgnoreCase) || deviceScreenSize.Equals("1920x1080", StringComparison.OrdinalIgnoreCase)) &&
                           deviceScreenDensity == 420) ||
                          (deviceApiLevel == 36 &&
                           (deviceScreenSize.Equals("1080x2424", StringComparison.OrdinalIgnoreCase) || deviceScreenSize.Equals("2424x1080", StringComparison.OrdinalIgnoreCase)) &&
                           deviceScreenDensity == 420)))
                    {
                        Assert.Fail(
                            $"Android visual tests should be run on an API30 emulator image with 1080x1920 420dpi screen or API36 emulator image with 1080x2424 420dpi screen, but the current device is API {deviceApiLevel} with a {deviceScreenSize} {deviceScreenDensity}dpi screen. Follow the steps on the MAUI UI testing wiki to launch the Android emulator with the right image."
                        );
                    }

                    break;

                case TestDevice.iOS:
                    var platformVersion = (string?) App.Capabilities.GetCapability("platformVersion")
                                          ?? throw new InvalidOperationException("platformVersion capability is missing or null.");

                    var device = (string?) App.Capabilities.GetCapability("deviceName")
                                 ?? throw new InvalidOperationException("deviceName capability is missing or null.");

                    environmentName = "ios";

                    if (device.Contains(" Xs", StringComparison.OrdinalIgnoreCase) && platformVersion == _iOSDefaultVersion)
                    {
                        environmentName = "ios";
                    }
                    else if (deviceName == "iPhone Xs (iOS 17.2)" || (device.Contains(" Xs", StringComparison.OrdinalIgnoreCase) && platformVersion == "17.2"))
                    {
                        environmentName = "ios";
                    }
                    else if (deviceName == "iPhone X (iOS 16.4)" || (device.Contains(" X", StringComparison.OrdinalIgnoreCase) && platformVersion == "16.4"))
                    {
                        environmentName = "ios-iphonex";
                    }
                    else
                    {
                        //Assert.Fail($"iOS visual tests should be run on iPhone Xs (iOS {_defaultiOSVersion}) or iPhone X (iOS 16.4) simulator images, but the current device is '{deviceName}' '{platformVersion}'. Follow the steps on the MAUI UI testing wiki.");
                    }

                    break;

                case TestDevice.Windows:
                    environmentName = "windows";

                    break;

                case TestDevice.Mac:
                    environmentName = "mac";

                    break;

                default:
                    throw new NotImplementedException($"Unknown device type {TestDevice}");
            }

            name ??= TestContext.Current.Test?.TestCase.TestMethodName ?? TestContext.Current.TestMethod?.MethodName ?? "UnknownTestName";

            // Currently Android is the OS with the ripple animations, but Windows may also have some animations
            // that need to finish before taking a screenshot.
            if (TestDevice == TestDevice.Android)
            {
                Thread.Sleep(350);
            }

#if MACCATALYST
			var screenshotPngBytes = TakeScreenshot() ?? throw new InvalidOperationException("Failed to get screenshot");
#else
            var screenshotPngBytes = App.GetScreenshot().AsByteArray ?? throw new InvalidOperationException("Failed to get screenshot");
#endif

            var actualImage = new ImageSnapshot(screenshotPngBytes, ImageSnapshotFormat.Png);

            // For Android and iOS, crop off the OS status bar at the top since it's not part of the
            // app itself and contains the time, which always changes. For WinUI, crop off the title
            // bar at the top as it varies slightly based on OS theme and is also not part of the app.
            var cropFromTop = TestDevice switch
            {
                TestDevice.Android => environmentName == "android-notch-36" ? 95u : 60u,
                TestDevice.iOS => environmentName == "ios-iphonex" ? 90u : 110u,
                TestDevice.Windows => 32u,
                TestDevice.Mac => 29u,
                _ => 0u,
            };

#if MACUITEST || WINTEST
				if (includeTitleBar)
				{
					cropFromTop = 0;
				}
#endif

            // For Android also crop the 3 button nav from the bottom, since it's not part of the
            // app itself and the button color can vary (the buttons change clear briefly when tapped).
            // For iOS, crop the home indicator at the bottom.
            var cropFromBottom = TestDevice switch
            {
                TestDevice.Android => environmentName == "android-notch-36" ? 40u : 125u,
                TestDevice.iOS => 40u,
                _ => 0u,
            };

            // Cropping from the left or right can be applied for any platform using the user-specified crop values.
            // The default values are set based on the platform, but the final cropping is determined by the parameters passed in.
            // This allows cropping of UI elements (such as navigation bars or home indicators) for any platform as needed.
            var cropFromLeft = 0u;
            var cropFromRight = 0u;

            cropFromLeft = cropLeft > 0 ? cropLeft : cropFromLeft;
            cropFromRight = cropRight > 0 ? cropRight : cropFromRight;
            cropFromTop = cropTop > 0 ? cropTop : cropFromTop;
            cropFromBottom = cropBottom > 0 ? cropBottom : cropFromBottom;

            if (cropFromLeft > 0 || cropFromRight > 0 || cropFromTop > 0 || cropFromBottom > 0)
            {
                var imageEditor = _imageEditorFactory.CreateImageEditor(actualImage);
                var (width, height) = imageEditor.GetSize();

                imageEditor.Crop((int)cropFromLeft, (int)cropFromTop, width - cropFromLeft - cropFromRight, height - cropFromTop - cropFromBottom);

                actualImage = imageEditor.GetUpdatedImage();
            }

            // Apply tolerance if specified
            if (tolerance > 0)
            {
                VerifyWithTolerance(name, actualImage, environmentName, tolerance);
            }
            else
            {
                _visualRegressionTester.VerifyMatchesSnapshot(name, actualImage, environmentName: environmentName, testContext: VisualTestUtilsTestContext.Current);
            }
        }
    }

    private void VerifyWithTolerance(string name, ImageSnapshot actualImage, string environmentName, double tolerance)
    {
        if (tolerance > 15)
        {
            throw new ArgumentException($"Tolerance {tolerance}% exceeds the acceptable limit. Please review whether this requires a different test or if it is a bug.");
        }

        try
        {
            _visualRegressionTester.VerifyMatchesSnapshot(name, actualImage, environmentName: environmentName, testContext: VisualTestUtilsTestContext.Current);
        }
        catch (Exception ex) when (IsVisualDifferenceException(ex))
        {
            var difference = ExtractDifferencePercentage(ex);

            if (difference <= tolerance)
            {
                // Log warning but pass test
                testOutputHelper.WriteLine($"Visual difference {difference}% within tolerance {tolerance}% for '{name}' on {environmentName}");

                return;
            }

            throw; // Re-throw if exceeds tolerance
        }
    }

    private bool IsVisualDifferenceException(Exception ex) =>
        // Check if this is a visual regression failure
        ex.GetType().Name.Contains("Assert", StringComparison.Ordinal) ||
        ex.Message.Contains("Snapshot different", StringComparison.Ordinal) ||
        ex.Message.Contains("baseline", StringComparison.Ordinal) ||
        ex.Message.Contains("different", StringComparison.Ordinal);

    private double ExtractDifferencePercentage(Exception ex)
    {
        var message = ex.Message;

        // Extract percentage from pattern: "X,XX% difference"
        var match = Regex.Match(message, @"(\d+,\d+)%\s*difference", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var percentageString = match.Groups[1].Value.Replace(',', '.');

            if (double.TryParse(percentageString, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
            {
                return percentage;
            }
        }

        // If can't extract specific percentage, throw an exception to indicate failure
        throw new InvalidOperationException("Unable to extract difference percentage from exception message.");
    }

#if MACCATALYST
    byte[] TakeScreenshot()
    {
        // Since the Appium screenshot on Mac (unlike Windows) is of the entire screen, not just the app,
        // we are going to crop the screenshot to the app window bounds, including rounded corners.
        var windowBounds = _app?.FindElement(AppiumQuery.ByXPath("//XCUIElementTypeWindow")).GetRect() ??
                           throw new InvalidOperationException("Failed to get app window bounds for screenshot cropping.");

        var x = windowBounds.X;
        var y = windowBounds.Y;
        var width = windowBounds.Width;
        var height = windowBounds.Height;
        const int cornerRadius = 12;

        // Take the screenshot
        var bytes = App.GetScreenshot().AsByteArray;

        // Draw a rounded rectangle with the app window bounds as mask
        using var surface = new MagickImage(MagickColors.Transparent, width, height);
        new Drawables()
            .RoundRectangle(0, 0, width, height, cornerRadius, cornerRadius)
            .FillColor(MagickColors.Black)
            .Draw(surface);

        // Composite the screenshot with the mask
        using var image = new MagickImage(bytes);
        surface.Composite(image, -x, -y, CompositeOperator.SrcAtop);

        return surface.ToByteArray(MagickFormat.Png);
    }
#endif
}
