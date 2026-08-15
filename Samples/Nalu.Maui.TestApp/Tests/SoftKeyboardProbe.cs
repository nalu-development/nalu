namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// PLATFORM ground truth for soft-keyboard assertions on APPLE platforms: a label reflecting the
/// live keyboard visibility, observed from the UIKit notifications.
/// </summary>
/// <remarks>
/// <para>
/// Android exposes keyboard state host-side (<c>adb shell dumpsys input_method</c>), so tests read
/// it without the app's help. iOS has no such channel, which is why the IME suites used to skip
/// there entirely — leaving the iOS half of features like <c>HideSoftInputOnTapped</c> (and the
/// host plumbing they are gated on) unguarded on the platform where it is implemented separately.
/// An in-app probe closes that gap, the same way the system-bar suites read their platform truth.
/// </para>
/// <para>
/// The notifications fire on the UI thread, so labels are updated directly. Registration happens
/// once per process and is never torn down: the observer outlives individual harness pages by
/// design, and every created label is tracked weakly so navigating away cannot leak it.
/// </para>
/// </remarks>
internal static class SoftKeyboardProbe
{
    /// <summary>Label text while the soft keyboard is up.</summary>
    public const string VisibleText = "keyboard:visible";

    /// <summary>Label text while the soft keyboard is down.</summary>
    public const string HiddenText = "keyboard:hidden";

    private static readonly List<WeakReference<Label>> _labels = [];
    private static bool _observing;
    private static bool _visible;

#if IOS || MACCATALYST
    // Held for the process lifetime: releasing the tokens would unsubscribe the observers.
    private static Foundation.NSObject? _willShowToken;
    private static Foundation.NSObject? _willHideToken;
#endif

    /// <summary>
    /// A label reporting the live keyboard visibility. Every page that a test observes the
    /// keyboard from needs its OWN label with a distinct id: scaffold-hosted pages stay in the
    /// element tree once visited, so a shared id would be ambiguous.
    /// </summary>
    public static Label CreateLabel(string automationId)
    {
        EnsureObserving();

        var label = new Label
        {
            Text = _visible ? VisibleText : HiddenText,
            AutomationId = automationId,
            FontSize = 11
        };

        _labels.Add(new WeakReference<Label>(label));

        return label;
    }

    private static void EnsureObserving()
    {
        if (_observing)
        {
            return;
        }

        _observing = true;

#if IOS || MACCATALYST
        _willShowToken = UIKit.UIKeyboard.Notifications.ObserveWillShow((_, _) => SetVisible(true));
        _willHideToken = UIKit.UIKeyboard.Notifications.ObserveWillHide((_, _) => SetVisible(false));
#endif
    }

    private static void SetVisible(bool visible)
    {
        _visible = visible;
        var text = visible ? VisibleText : HiddenText;

        for (var i = _labels.Count - 1; i >= 0; i--)
        {
            if (_labels[i].TryGetTarget(out var label))
            {
                label.Text = text;
            }
            else
            {
                _labels.RemoveAt(i);
            }
        }
    }

    #region Keyboard height probe

    /// <summary>Text prefix of a height probe label: <c>kb:&lt;height in dp&gt;</c>.</summary>
    public const string HeightPrefix = "kb:";

    private static readonly List<WeakReference<Label>> _heightLabels = [];
    private static bool _observingHeight;
    private static double _height;

    /// <summary>
    /// A label reporting the soft keyboard's live overlap with the app window (device-independent
    /// units, 0 while hidden) — PLATFORM ground truth, read outside the library under test: the
    /// UIKit keyboard frame notifications on Apple platforms, the root IME window insets on
    /// Android (polled — the DecorView is the only place they are always current).
    /// </summary>
    public static Label CreateHeightLabel(string automationId)
    {
        EnsureObservingHeight();

        var label = new Label
        {
            Text = FormatHeight(_height),
            AutomationId = automationId,
            FontSize = 11
        };

        _heightLabels.Add(new WeakReference<Label>(label));

        return label;
    }

    private static string FormatHeight(double height) => $"{HeightPrefix}{height:0}";

    private static void EnsureObservingHeight()
    {
        if (_observingHeight)
        {
            return;
        }

        _observingHeight = true;

#if IOS || MACCATALYST
        _frameToken = UIKit.UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            var window = UIKit.UIApplication.SharedApplication.ConnectedScenes
                                .OfType<UIKit.UIWindowScene>()
                                .SelectMany(scene => scene.Windows)
                                .FirstOrDefault(candidate => candidate.IsKeyWindow);

            var overlap = window is null ? 0 : Math.Max(0, (double)(window.Bounds.Bottom - args.FrameEnd.Top));
            SetHeight(overlap);
        });

        _willHideToken2 = UIKit.UIKeyboard.Notifications.ObserveWillHide((_, _) => SetHeight(0));
#elif ANDROID
        Application.Current!.Dispatcher.StartTimer(TimeSpan.FromMilliseconds(100), () =>
        {
            if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.DecorView is { } decor
                && AndroidX.Core.View.ViewCompat.GetRootWindowInsets(decor) is { } insets)
            {
                var ime = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());
                SetHeight(Microsoft.Maui.Platform.ContextExtensions.FromPixels(decor.Context!, ime?.Bottom ?? 0));
            }

            return true;
        });
#endif
    }

#if IOS || MACCATALYST
    private static Foundation.NSObject? _frameToken;
    private static Foundation.NSObject? _willHideToken2;
#endif

    private static void SetHeight(double height)
    {
        if (Math.Abs(height - _height) < 0.5)
        {
            return;
        }

        _height = height;
        var text = FormatHeight(height);

        for (var i = _heightLabels.Count - 1; i >= 0; i--)
        {
            if (_heightLabels[i].TryGetTarget(out var label))
            {
                label.Text = text;
            }
            else
            {
                _heightLabels.RemoveAt(i);
            }
        }
    }

    #endregion
}
