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
}
