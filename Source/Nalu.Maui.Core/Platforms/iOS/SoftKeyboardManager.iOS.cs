using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using UIKit;
using ContentView = Microsoft.Maui.Platform.ContentView;
// ReSharper disable NotAccessedField.Local
#pragma warning disable CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS0169 // Field is never used

// ReSharper disable InconsistentNaming

namespace Nalu;

/// <summary>
/// Manager for handling soft keyboard adjustments.
/// </summary>
[SuppressMessage("Style", "IDE0022:Use expression body for method")]
public static partial class SoftKeyboardManager
{
    // private static bool _textViewScrollable;
    private static Action? _windowBackgroundColorReset;
    private static CGRect _textViewBounds;
    private static nfloat _lastPan;
    private static UIView? _rootView;
    private static UIEdgeInsets _initialAdditionalSafeAreaInsets = UIEdgeInsets.Zero;
    private static NSLayoutConstraint? _scrollViewBottomInsetConstraint;
    private static UIScrollView? _scrollView;
    private static UIWindow? _applicationWindow;

    private static NSObject? _willShowToken;
    private static NSObject? _willHideToken;
    private static NSObject? _textFieldToken;
    private static NSObject? _textFieldEndToken;
    private static NSObject? _textViewToken;
    private static NSObject? _textViewEndToken;
    private static NSObject? _orientationChangeToken;
    private static NSObject? _didChangeFrameToken;
    private static DispatchSource.Timer? _textViewBoundsWatcherTimer;
    private static bool _textViewBoundsWatcher;
    private static nfloat _textViewHeight;
    private static UIView? _textView;
    private static UIViewController? _pageViewController;
    private static NSLayoutConstraint[]? _containerViewConstraints;
    private static double? _resizeDelta;
    private static SoftKeyboardAdjustMode _adjustMode = SoftKeyboardAdjustMode.Resize;
    private static double _animationDuration = 0.25;
    private static CGRect _keyboardFrame;
    private static double _screenWidth;
    private static bool _adjusted;
    private static bool _editing;
    private static bool _shown;
    
    private static UIWindow ApplicationWindow => _applicationWindow ??= GetApplicationWindow() ?? throw new InvalidOperationException("Could not find application window.");

    internal static void Configure(MauiAppBuilder builder)
        => builder.ConfigureLifecycleEvents(events => events.AddiOS(Configure));

    private static void Configure(this IiOSLifecycleBuilder lifecycle)
        => lifecycle.FinishedLaunching((_, _) =>
            {
                // Disconnect the built-in keyboard auto manager
                KeyboardAutoManagerScroll.Disconnect();
                // Connect the Nalu soft keyboard manager
                Connect();

                return true;
            }
        );

    private static void Connect()
    {
        _textFieldToken = NSNotificationCenter.DefaultCenter.AddObserver(UITextField.TextDidBeginEditingNotification, DidUITextBeginEditing);
        _textFieldEndToken = NSNotificationCenter.DefaultCenter.AddObserver(UITextField.TextDidEndEditingNotification, DidUITextViewEndEditing);
        _textViewToken = NSNotificationCenter.DefaultCenter.AddObserver(UITextView.TextDidBeginEditingNotification, DidUITextBeginEditing);
        _textViewEndToken = NSNotificationCenter.DefaultCenter.AddObserver(UITextView.TextDidEndEditingNotification, DidUITextViewEndEditing);
        _willShowToken = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, WillKeyboardShow);
        _willHideToken = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, WillHideKeyboard);
        _didChangeFrameToken = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.DidChangeFrameNotification, DidChangeFrame);
        _orientationChangeToken = NSNotificationCenter.DefaultCenter.AddObserver(UIDevice.OrientationDidChangeNotification, OrientationChanged);
        _textViewBoundsWatcherTimer = new DispatchSource.Timer(DispatchQueue.MainQueue);
        _textViewBoundsWatcherTimer.SetTimer(DispatchTime.Now, 300_000, 0);
        _textViewBoundsWatcherTimer.SetEventHandler(OnTextViewBoundsWatcherTick);
    }

    static partial void DumpInfo(string message);

    static partial void DumpNotification(NSNotification notification);

    private static void OnTextViewBoundsWatcherTick()
    {
        if (_textView is { } textView && !textView.Bounds.Equals(_textViewBounds))
        {
            DumpInfo("TextView bounds changed.");
            _textViewBounds = textView.Bounds;
            Adjust();
        }
    }

    private static void OrientationChanged(NSNotification obj)
    {
        DumpNotification(obj);
    }
    
    private static void OnKeyboardFrameChanged(NSDictionary userInfo)
    {
        var frameDescription = userInfo.GetValueOrDefault(UIKeyboard.FrameEndUserInfoKey)?.Description;
        var frame = DescriptionToCGRect(frameDescription) ?? CGRect.Empty;
        _keyboardFrame = _shown ? frame : CGRect.Empty;
        SetAnimationDuration(userInfo);
        Adjust();
    }

    private static void Adjust()
    {
        var frame = _keyboardFrame;
        var targetHeight = _keyboardFrame.Height;
        var transform = CGAffineTransform.MakeIdentity();

        if (_pageViewController is not null)
        {
            var rootView = _rootView ?? throw new InvalidOperationException("The root view is null.");
            var pageView = _pageViewController.View ?? throw new InvalidOperationException("The page view controller has no view.");
            
            if (targetHeight != 0)
            {
                var window = pageView.Window ?? GetApplicationWindow() ?? throw new InvalidOperationException("Could not find application window.");
#pragma warning disable CA1416 // Dereference of a possibly null reference.
                if ((OperatingSystem.IsIOSVersionAtLeast(13) || OperatingSystem.IsMacCatalystVersionAtLeast(13)) && window.WindowScene is { } windowScene)
                {
                    // Account for split-over
                    frame = window.ConvertRectFromCoordinateSpace(frame, windowScene.Screen.CoordinateSpace);
                }
#pragma warning restore CA1416 // Dereference of a possibly null reference.

                frame = CGRect.Intersect(frame, window.Frame);

                var keyboardFrameInWindow = CGRect.Intersect(frame, window.Frame);

                if (_adjustMode == SoftKeyboardAdjustMode.Pan)
                {
                    var cursorInWindow = FindCursorPosition() ?? CGRect.Empty;
                    var deltaY = rootView.Center.Y - (rootView.Bounds.Height / 2) - rootView.Frame.Y;
                    var cursorBottom = cursorInWindow.Bottom + deltaY;
                    targetHeight = cursorBottom < keyboardFrameInWindow.Top
                        ? 0
                        : (nfloat)Math.Min(keyboardFrameInWindow.Height, cursorBottom - keyboardFrameInWindow.Top + 20); // Add a little padding

                    if (_lastPan == targetHeight)
                    {
                        return;
                    }

                    _lastPan = targetHeight;
                }
                else if (_adjustMode == SoftKeyboardAdjustMode.Resize)
                {
                    _lastPan = 0;

                    var originalContainerViewFrameInWindow = pageView.ConvertRectToView(pageView.Frame, null);
                    var keyboardIntersection = CGRect.Intersect(originalContainerViewFrameInWindow, keyboardFrameInWindow);
                    var superviewSafeAreaInsets = pageView.Superview?.SafeAreaInsets ?? UIEdgeInsets.Zero;
                    targetHeight = keyboardIntersection.Height - superviewSafeAreaInsets.Bottom;
                }
                else
                {
                    targetHeight = 0;
                    _lastPan = 0;
                }
            }

            if (_adjustMode == SoftKeyboardAdjustMode.Resize)
            {
                var initialInsets = _initialAdditionalSafeAreaInsets;

                var newInsets = new UIEdgeInsets(
                    initialInsets.Top,
                    initialInsets.Left,
                    initialInsets.Bottom + targetHeight,
                    initialInsets.Right
                );

                _pageViewController.AdditionalSafeAreaInsets = newInsets;
            }
            else if (_adjustMode == SoftKeyboardAdjustMode.Pan)
            {
                _pageViewController.AdditionalSafeAreaInsets = _initialAdditionalSafeAreaInsets;
                transform = CGAffineTransform.MakeTranslation(0, -targetHeight);
            }

            UIView.Animate(
                _animationDuration,
                () =>
                {
                    State.IsVisible = _shown;
                    _rootView?.Transform = transform;
                    ApplicationWindow.LayoutIfNeeded();
                }
            );
        }
    }

    private static void DidUITextBeginEditing(NSNotification notification)
    {
        DumpNotification(notification);
        _textView = notification.Object as UIView ?? throw new InvalidOperationException("Notification object is not a UIView.");
        _textViewBounds = _textView.Bounds;

        // if (_textView is UIScrollView scrollableTextView)
        // {
        //     _textViewScrollable = scrollableTextView.ScrollEnabled;
        //     if (_textView.FindPlatformResponder<UIScrollView>() is not null)
        //     {
        //         scrollableTextView.ScrollEnabled = false;
        //     }
        // }

        if (!_textViewBoundsWatcher)
        {
            _textViewBoundsWatcherTimer!.Resume();
            _textViewBoundsWatcher = true;
        }

        // If we cannot find a page view controller, we just fake one to avoid forks in the code
        var pageViewController = _textView.GetContainerPlatformViewController() ?? new UIViewController();
        if (_pageViewController is not null && !ReferenceEquals(_pageViewController, pageViewController))
        {
            // Reset previous page controller insets
            _pageViewController.AdditionalSafeAreaInsets = _initialAdditionalSafeAreaInsets;
            _pageViewController = null;
            var rootView = _rootView ?? throw new InvalidOperationException("The root view should not be null here.");
            rootView.Transform = CGAffineTransform.MakeIdentity();
            _rootView = null;
            _windowBackgroundColorReset?.Invoke();
            _windowBackgroundColorReset = null;
        }

        if (_pageViewController is null)
        {
            _pageViewController = pageViewController;
            _initialAdditionalSafeAreaInsets = _pageViewController.AdditionalSafeAreaInsets;
            var window = _textView.Window ?? throw new InvalidOperationException("Could not find text view's window.");
            var weakWindow = new WeakReference<UIWindow>(window);
            var originalBackgroundColor = window.BackgroundColor;
            _windowBackgroundColorReset = () =>
            {
                if (weakWindow.TryGetTarget(out var targetWindow))
                {
                    targetWindow.BackgroundColor = originalBackgroundColor;
                }
            };
            window.BackgroundColor = _pageViewController.View!.BackgroundColor ?? UIColor.SystemGray;
            _rootView ??= window?.RootViewController?.View ?? throw new InvalidOperationException("Could not find root view.");
        }

        var parent = _textView;
        var adjustMode = DefaultAdjustMode;
        while (parent != null)
        {
            if (_adjustRuleViews.TryGetValue(parent, out var adjustModeBox))
            {
                adjustMode = adjustModeBox.Value;
                break;
            }

            parent = parent.Superview;
        }

        _adjustMode = adjustMode;
        State.AdjustMode = adjustMode;
        
        if (_shown)
        {
            Adjust();
        }
    }

    private static void DidUITextViewEndEditing(NSNotification notification)
    {
        DumpNotification(notification);
        
        // if (_textView is UIScrollView scrollableTextView)
        // {
        //     scrollableTextView.ScrollEnabled = _textViewScrollable;
        // }

        if (_textViewBoundsWatcher)
        {
            _textViewBoundsWatcherTimer!.Suspend();
            _textViewBoundsWatcher = false;
        }

        if (!_shown)
        {
            _pageViewController = null;
            _initialAdditionalSafeAreaInsets = UIEdgeInsets.Zero;
            _adjustMode = DefaultAdjustMode;
            State.AdjustMode = DefaultAdjustMode;
            _windowBackgroundColorReset?.Invoke();
            _windowBackgroundColorReset = null;
            _textView = null;
            _pageViewController = null;
            _rootView = null;
        }
    }

    private static void WillKeyboardShow(NSNotification notification)
    {
        DumpNotification(notification);
        _shown = true;

        if (notification.UserInfo is { } userInfo)
        {
            OnKeyboardFrameChanged(userInfo);
        }
    }

    private static void WillHideKeyboard(NSNotification notification)
    {
        DumpNotification(notification);
        _shown = false;
        
        if (notification.UserInfo is { } userInfo)
        {
            OnKeyboardFrameChanged(userInfo);
        }
    }

    private static void DidChangeFrame(NSNotification notification)
    {
        DumpNotification(notification);
        if (notification.UserInfo is { } userInfo)
        {
            OnKeyboardFrameChanged(userInfo);
        }
    }

    private static UIWindow? GetApplicationWindow()
        => Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as UIWindow;

    // Used to get the numeric values from the UserInfo dictionary's NSObject value to CGRect.
    // Doing manually since CGRectFromString is not yet bound
    private static CGRect? DescriptionToCGRect(string? description)
    {
        // example of passed in description: "NSRect: {{0, 586}, {430, 346}}"

        if (description is null)
        {
            return null;
        }

        var temp = RemoveEverythingExceptForNumbersAndCommas(description);
        var dimensions = temp.Split(',');

        if (dimensions.Length == 4
            && nfloat.TryParse(dimensions[0], CultureInfo.InvariantCulture, out var x)
            && nfloat.TryParse(dimensions[1], CultureInfo.InvariantCulture, out var y)
            && nfloat.TryParse(dimensions[2], CultureInfo.InvariantCulture, out var width)
            && nfloat.TryParse(dimensions[3], CultureInfo.InvariantCulture, out var height))
        {
            return new CGRect(x, y, width, height);
        }

        return null;

        static string RemoveEverythingExceptForNumbersAndCommas(string input)
        {
            var sb = new StringBuilder(input.Length);

            foreach (var character in input)
            {
                if (char.IsDigit(character) || character == ',' || character == '.')
                {
                    sb.Append(character);
                }
            }

            return sb.ToString();
        }
    }

    private static NSObject? GetValueOrDefault(this NSDictionary dict, string key)
    {
        using var keyName = new NSString(key);
        dict.TryGetValue(keyName, out var obj);

        return obj;
    }

    private static void SetAnimationDuration(this NSDictionary dict)
    {
        var durationObj = dict.GetValueOrDefault("UIKeyboardAnimationDurationUserInfoKey");
        var durationNum = (NSNumber) NSObject.FromObject(durationObj);
        var num = (double) durationNum;

        if (num != 0)
        {
            _animationDuration = num;
        }
    }

    private static CGRect? FindLocalCursorPosition()
    {
        var textInput = _textView as IUITextInput;
        var selectedTextRange = textInput?.SelectedTextRange;

        return selectedTextRange is not null ? textInput?.GetCaretRectForPosition(selectedTextRange.Start) : null;
    }

    private static CGRect? FindCursorPosition()
    {
        var localCursor = FindLocalCursorPosition();
        
        if (localCursor is { } local && _pageViewController is { View: { } containerView })
        {
            var cursorInContainer = containerView.ConvertRectFromView(local, _textView);
            var cursorInWindow = containerView.ConvertRectToView(cursorInContainer, null);
        
            return cursorInWindow;
        }

        return null;
    }

    private static UIView? GetContainerPlatformView(this UIView? startingPoint)
    {
        var rootView = startingPoint?.FindPlatformResponder<ContainerViewController>()?.View;

        if (rootView is not null)
        {
            return rootView;
        }

        var firstViewController = startingPoint?.FindTopController<UIViewController>();

        return firstViewController?.ViewIfLoaded?.FindDescendantView<ContentView>();
    }
    
    private static UIViewController? GetContainerPlatformViewController(this UIView? startingPoint)
    {
        var rootViewController = startingPoint?.FindPlatformResponder<ContainerViewController>();

        if (rootViewController is not null)
        {
            return rootViewController;
        }

        // Not a MAUI page, get the nearest UIViewController
        var firstViewController = startingPoint?.FindPlatformResponder<UIViewController>();

        return firstViewController;
    }

    internal static T? FindTopController<T>(this UIView view)
        where T : UIViewController
    {
        var bestController = view.FindPlatformResponder<T>();
        var tempController = bestController;

        while (tempController is not null)
        {
            tempController = tempController.FindPlatformResponder<T>();

            if (tempController is not null)
            {
                bestController = tempController;
            }
        }

        return bestController;
    }

    internal static T? FindPlatformResponder<T>(this UIViewController controller)
        where T : UIViewController
    {
        var nextResponder = controller.View as UIResponder;

        while (nextResponder is not null)
        {
            // We check for Window to avoid scenarios where an invalidate might propagate up the tree
            // To a SuperView that's been disposed which will cause a crash when trying to access it
            if (nextResponder is UIView { Window: null })
            {
                return null;
            }

            nextResponder = nextResponder.NextResponder;

            if (nextResponder is T responder && !ReferenceEquals(responder, controller))
            {
                return responder;
            }
        }

        return null;
    }

    private static T? FindPlatformResponder<T>(this UIView view)
        where T : UIResponder
    {
        UIResponder? nextResponder = view;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        while (nextResponder is not null)
        {
            // We check for Window to avoid scenarios where an invalidate might propagate up the tree
            // To a SuperView that's been disposed which will cause a crash when trying to access it
            if (nextResponder is UIView { Window: null })
            {
                return null;
            }

            nextResponder = nextResponder.NextResponder;

            if (nextResponder is T responder)
            {
                return responder;
            }
        }

        return null;
    }
}
