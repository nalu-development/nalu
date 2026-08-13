namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// PLATFORM ground truth for safe-area assertions: the WINDOW's system insets in DIPs, read
/// natively (iOS <c>UIWindow.SafeAreaInsets</c>, Android <c>WindowInsetsCompat.Type.SystemBars</c>)
/// — the values MAUI's <c>SafeAreaEdges</c> is supposed to consume.
/// </summary>
/// <remarks>
/// <para>
/// Geometry assertions that merely TOLERATE a zero inset ("the bar reaches the bottom edge",
/// "the bar is at least as tall as its content") pass on a device without system insets AND
/// after a regression that drops the inset contribution altogether. Reading the real value lets
/// a test assert the exact contribution, and skip explicitly when the device has none instead of
/// passing vacuously.
/// </para>
/// <para>
/// Values are whole DIPs formatted invariantly on purpose: the agent serializes fractional
/// numbers with the DEVICE locale (see the ScaffoldNavBarAppearanceTests helper), and integer
/// insets are well within the ±1.5 tolerance layout assertions already use.
/// </para>
/// </remarks>
internal static class SafeAreaProbe
{
    /// <summary>Reserved AutomationId of the probe's refresh button.</summary>
    public const string ButtonAutomationId = "SafeAreaProbeButton";

    /// <summary>Reserved AutomationId of the probe's value label.</summary>
    public const string ValueAutomationId = "SafeAreaProbeValue";

    /// <summary>
    /// Reserved AutomationId of the EFFECTIVE page insets label: what the hosted page actually
    /// sees (system safe area plus the chrome contribution the scaffold adds on top of it), as
    /// opposed to the window insets reported by <see cref="ValueAutomationId" />.
    /// </summary>
    public const string PageValueAutomationId = "SafeAreaPageProbeValue";

    /// <summary>The value rendered before the first refresh (and when the view is not realized).</summary>
    public const string Unavailable = "n/a";

    /// <summary>
    /// A refresh button + value label pair, ready to drop into a harness page. The value is
    /// re-read on every tap: insets change with rotation, chrome and the IME, so a test reads
    /// them at the moment it needs them rather than trusting a boot-time snapshot.
    /// </summary>
    public static View CreateProbe(VisualElement anchor)
    {
        var value = new Label
        {
            Text = Unavailable,
            AutomationId = ValueAutomationId,
            FontSize = 11,
            VerticalTextAlignment = TextAlignment.Center
        };

        var pageValue = new Label
        {
            Text = Unavailable,
            AutomationId = PageValueAutomationId,
            FontSize = 11,
            VerticalTextAlignment = TextAlignment.Center
        };

        var button = new Button
        {
            Text = "Insets",
            AutomationId = ButtonAutomationId,
            FontSize = 11
        };

        button.Clicked += (_, _) =>
        {
            value.Text = Measure(anchor);
            pageValue.Text = MeasurePage(anchor);
        };

        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { button, value, pageValue }
        };
    }

    /// <summary>
    /// The window system insets around <paramref name="anchor" />, as
    /// <c>"L:{left} T:{top} R:{right} B:{bottom}"</c> in whole DIPs, or <see cref="Unavailable" />
    /// when the anchor has no realized platform view yet.
    /// </summary>
    public static string Measure(VisualElement anchor)
    {
#if IOS || MACCATALYST
        if (anchor.Handler?.PlatformView is not UIKit.UIView platformView || platformView.Window is not { } window)
        {
            return Unavailable;
        }

        var insets = window.SafeAreaInsets;

        return Format(insets.Left, insets.Top, insets.Right, insets.Bottom);
#elif ANDROID
        if (anchor.Handler?.PlatformView is not Android.Views.View platformView)
        {
            return Unavailable;
        }

        var rootInsets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(platformView);

        if (rootInsets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars()) is not { } insets)
        {
            return Unavailable;
        }

        var density = platformView.Resources!.DisplayMetrics!.Density;

        return Format(insets.Left / density, insets.Top / density, insets.Right / density, insets.Bottom / density);
#else
        _ = anchor;

        return Unavailable;
#endif
    }

    /// <summary>
    /// The insets the hosted PAGE effectively sees: the system safe area plus whatever the host
    /// adds on top of it (on iOS, the chrome contribution carried by
    /// <c>UIViewController.AdditionalSafeAreaInsets</c>, which is ADDITIVE — a zero contribution
    /// still leaves the page the native inset).
    /// </summary>
    public static string MeasurePage(VisualElement anchor)
    {
#if IOS || MACCATALYST
        if (anchor.Handler?.PlatformView is not UIKit.UIView platformView)
        {
            return Unavailable;
        }

        var insets = platformView.SafeAreaInsets;

        return Format(insets.Left, insets.Top, insets.Right, insets.Bottom);
#elif ANDROID
        if (anchor.Handler?.PlatformView is not Android.Views.View platformView)
        {
            return Unavailable;
        }

        // No meaningful Android reading: the host REWRITES the SystemBars insets it dispatches
        // into the page subtree (see ScaffoldPageLayerLayout.Rewrite), but whether MAUI turns them
        // into padding — and on WHICH view — depends on each view's SafeAreaEdges. Reporting that
        // padding would encode an interpretation of the declaration, exactly what the chrome
        // measurement avoids. Assert the observable instead: content stopping above the inset
        // (see ScaffoldNavBarChromeTests.PageKeepsTheNativeBottomInsetWithoutATabBar).
        _ = platformView;

        return Unavailable;
#else
        _ = anchor;

        return Unavailable;
#endif
    }

    private static string Format(double left, double top, double right, double bottom)
        => $"L:{(int) Math.Round(left)} T:{(int) Math.Round(top)} R:{(int) Math.Round(right)} B:{(int) Math.Round(bottom)}";
}
