namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// In-app device rotation for the orientation suites, plus a label reporting the orientation the
/// app actually ended up in.
/// </summary>
/// <remarks>
/// <para>
/// Rotation has to come from INSIDE the app: no host-side tool can rotate an iOS simulator
/// (<c>simctl</c> has no such command, <c>axe</c> has no rotation subcommand, and driving the
/// Simulator's ⌘→ shortcut needs Accessibility trust the test host does not have). Android could be
/// rotated with <c>adb settings put system user_rotation</c>, but keeping one mechanism for both
/// platforms means a test reads the same on either.
/// </para>
/// <para>
/// iOS asks the window scene for a geometry update (the same API MAUI itself uses); Android sets
/// the activity's requested orientation. Both are requests, not commands — which is why the label
/// reports what the app ACTUALLY got, and tests wait for the window to flip rather than assuming
/// the rotation took.
/// </para>
/// </remarks>
internal static class OrientationProbe
{
    /// <summary>Reserved AutomationIds of the rotation controls and the state label.</summary>
    public const string PortraitButtonAutomationId = "OrientationPortraitButton";

    public const string LandscapeButtonAutomationId = "OrientationLandscapeButton";

    public const string ValueAutomationId = "OrientationValue";

    /// <summary>Rotation controls plus the live orientation label, ready to drop into a harness page.</summary>
    public static View CreateControls()
    {
        var value = new Label
        {
            AutomationId = ValueAutomationId,
            FontSize = 11,
            VerticalTextAlignment = TextAlignment.Center
        };

        void Refresh() => value.Text = Current();

        // Reactive, not tap-driven: a rotation REQUEST returns long before the window flips, so a
        // label refreshed at tap time would report the old orientation.
        void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => value.Dispatcher.Dispatch(Refresh);

        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
        value.Unloaded += (_, _) => DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;

        var portrait = new Button { Text = "Portrait", AutomationId = PortraitButtonAutomationId, FontSize = 11 };
        var landscape = new Button { Text = "Landscape", AutomationId = LandscapeButtonAutomationId, FontSize = 11 };

        portrait.Clicked += (_, _) => Request(landscape: false);
        landscape.Clicked += (_, _) => Request(landscape: true);

        Refresh();

        var controls = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { portrait, landscape, value }
        };

        // Belt and braces: the display-changed event is not raised dependably on Android when the
        // activity handles the configuration change itself, but the controls DO get resized by the
        // rotation — so their own size change is a signal that always arrives.
        controls.SizeChanged += (_, _) => Refresh();

        return controls;
    }

    /// <summary>
    /// "orientation:landscape" / "orientation:portrait", from the display's reported ORIENTATION.
    /// </summary>
    /// <remarks>
    /// Not from Width vs Height: on Android those are the display's raw dimensions and do not swap
    /// when the device rotates, so a size comparison reports portrait forever.
    /// </remarks>
    public static string Current()
        => DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape
            ? "orientation:landscape"
            : "orientation:portrait";

    /// <summary>Asks the platform to rotate. Both platforms may decline (locked device, unsupported orientation).</summary>
    public static void Request(bool landscape)
    {
#if IOS
        if (UIKit.UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault() is not UIKit.UIWindowScene scene)
        {
            return;
        }

        var mask = landscape ? UIKit.UIInterfaceOrientationMask.LandscapeRight : UIKit.UIInterfaceOrientationMask.Portrait;

        scene.RequestGeometryUpdate(
            new UIKit.UIWindowSceneGeometryPreferencesIOS(mask),
            _ =>
            {
                // Declined requests surface through the orientation label, not an exception.
            }
        );
#elif ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is { } activity)
        {
            activity.RequestedOrientation = landscape
                ? Android.Content.PM.ScreenOrientation.Landscape
                : Android.Content.PM.ScreenOrientation.Portrait;
        }
#else
        _ = landscape;
#endif
    }
}
