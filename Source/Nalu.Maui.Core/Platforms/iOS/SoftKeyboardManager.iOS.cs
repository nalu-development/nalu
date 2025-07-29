using System.Globalization;
using System.Reflection;
using System.Text;
using CoreFoundation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using UIKit;
using ContentView = Microsoft.Maui.Platform.ContentView;
// ReSharper disable NotAccessedField.Local

// ReSharper disable InconsistentNaming

namespace Nalu;

/// <summary>
/// Manager for handling soft keyboard adjustments.
/// </summary>
public static partial class SoftKeyboardManager
{
    private const int _textViewDistanceFromBottom = 20;

    private static readonly FieldInfo? _isKeyboardAutoScrollHandlingField
        = typeof(KeyboardAutoManagerScroll).GetField("IsKeyboardAutoScrollHandling", BindingFlags.NonPublic);

    private static readonly UIInterfaceOrientationMask _supportedOrientations = GetSupportedOrientations();
    private static NSObject? _willShowToken;
    private static NSObject? _willHideToken;
    private static NSObject? _textFieldToken;
    private static NSObject? _textFieldEndToken;
    private static NSObject? _textViewToken;
    private static NSObject? _textViewEndToken;
    private static NSObject? _orientationChangeToken;
    private static NSObject? _didChangeFrameToken;
    private static DispatchSource.Timer? _textViewResizedTimer;
    private static bool _textViewResizedTimerRunning;
    private static nfloat _textViewHeight;
    private static UIView? _textView;
    private static UIView? _rootView;
    private static UIView? _containerView;
    private static NSLayoutConstraint[]? _containerViewConstraints;
    private static double? _resizeDelta;
    private static double? _panDelta;
    private static SoftKeyboardAdjustMode _adjustMode = SoftKeyboardAdjustMode.Resize;
    private static double _animationDuration = 0.25;
    private static CGRect _keyboardFrame;
    private static double _screenWidth;
    private static bool _adjusted;
    private static bool _editing;
    private static bool _shown;

    private static bool MauiKeyboardScrollManagerHandlingFlag
    {
        get => _isKeyboardAutoScrollHandlingField?.GetValue(null) as bool? ?? false;
        set => _isKeyboardAutoScrollHandlingField?.SetValue(null, value);
    }

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
        _textViewResizedTimer = new DispatchSource.Timer(DispatchQueue.MainQueue);
        _textViewResizedTimer.SetTimer(DispatchTime.Now, 300_000, 0);
        _textViewResizedTimer.SetEventHandler(CheckTextViewResized);
    }

    static partial void DumpInfo(string message);

    static partial void DumpNotification(NSNotification notification);

    private static void CheckTextViewResized()
    {
        if (_textView is not null && _textViewHeight != _textView.Frame.Height)
        {
            _textViewHeight = _textView.Frame.Height;
            Adjust();
        }
    }

    private static void OrientationChanged(NSNotification obj)
    {
        DumpNotification(obj);
        var nativeSize = UIScreen.MainScreen.Bounds.Size;
        var orientation = UIDevice.CurrentDevice.Orientation;

        if (_screenWidth == 0)
        {
            _screenWidth = nativeSize.Width;
            return;
        }

        if (orientation == UIDeviceOrientation.Portrait && !_supportedOrientations.HasFlag(UIInterfaceOrientationMask.Portrait) ||
            orientation == UIDeviceOrientation.PortraitUpsideDown && !_supportedOrientations.HasFlag(UIInterfaceOrientationMask.PortraitUpsideDown) ||
            orientation == UIDeviceOrientation.LandscapeLeft && !_supportedOrientations.HasFlag(UIInterfaceOrientationMask.LandscapeLeft) ||
            orientation == UIDeviceOrientation.LandscapeRight && !_supportedOrientations.HasFlag(UIInterfaceOrientationMask.LandscapeRight))
        {
            return;
        }
        

        var width = orientation switch
        {
            UIDeviceOrientation.LandscapeLeft or UIDeviceOrientation.LandscapeRight => Math.Max(nativeSize.Height, nativeSize.Width),
            UIDeviceOrientation.Portrait or UIDeviceOrientation.PortraitUpsideDown => Math.Min(nativeSize.Height, nativeSize.Width),
            _ => _screenWidth
        };

        _screenWidth = width;
    }
    
    private static UIInterfaceOrientationMask GetSupportedOrientations()
    {
        if (NSBundle.MainBundle.InfoDictionary["UISupportedInterfaceOrientations"] is not NSArray orientations)
        {
            return UIInterfaceOrientationMask.All;
        }

        var mask = (UIInterfaceOrientationMask)0;

        foreach (var item in orientations)
        {
            switch (item.ToString())
            {
                case "UIInterfaceOrientationPortrait":
                    mask |= UIInterfaceOrientationMask.Portrait;
                    break;
                case "UIInterfaceOrientationPortraitUpsideDown":
                    mask |= UIInterfaceOrientationMask.PortraitUpsideDown;
                    break;
                case "UIInterfaceOrientationLandscapeLeft":
                    mask |= UIInterfaceOrientationMask.LandscapeLeft;
                    break;
                case "UIInterfaceOrientationLandscapeRight":
                    mask |= UIInterfaceOrientationMask.LandscapeRight;
                    break;
            }
        }

        return mask;
    }


    private static void DidUITextBeginEditing(NSNotification notification)
    {
        DumpNotification(notification);
        _editing = true;

        if (notification.Object is UIView view)
        {
            _textView = view;
            _textViewHeight = view.Frame.Height;

            if (!_textViewResizedTimerRunning)
            {
                _textViewResizedTimer!.Resume();
                _textViewResizedTimerRunning = true;
            }

            var parent = view;
            var containerView = view.GetContainerPlatformView();

            // When switching to a new container view with the keyboard already visible and the adjust mode was Resize,
            // we need to restore the previous container view's frame.
            if (_adjustMode == SoftKeyboardAdjustMode.Resize &&
                _containerViewConstraints is not null &&
                _containerView is not null &&
                !ReferenceEquals(containerView, _containerView))
            {
                RestoreContainerView();
                NSLayoutConstraint.DeactivateConstraints(_containerViewConstraints);
                _containerView.TranslatesAutoresizingMaskIntoConstraints = true;
                _containerViewConstraints = null;
                _containerView = null;
            }

            if (_containerView is null && containerView is { Superview: { } containerViewSuperview })
            {
                _containerView = containerView;

                _containerView.TranslatesAutoresizingMaskIntoConstraints = false;
                _containerViewConstraints = [
                    _containerView.BottomAnchor.ConstraintEqualTo(containerViewSuperview.BottomAnchor),
                    _containerView.TopAnchor.ConstraintEqualTo(containerViewSuperview.TopAnchor),
                    _containerView.LeadingAnchor.ConstraintEqualTo(containerViewSuperview.LeadingAnchor),
                    _containerView.TrailingAnchor.ConstraintEqualTo(containerViewSuperview.TrailingAnchor)
                ];

                NSLayoutConstraint.ActivateConstraints(
                    _containerViewConstraints
                );
            }

            _rootView ??= _containerView?.Window.RootViewController?.View;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            while (parent != null)
            {
                if (_adjustRuleViews.TryGetValue(parent, out var adjustModeBox))
                {
                    _adjustMode = adjustModeBox.Value;
                    State.AdjustMode = _adjustMode;

                    return;
                }

                parent = parent.Superview;
            }

            _adjustMode = DefaultAdjustMode;
            State.AdjustMode = _adjustMode;
        }
    }

    private static void DidUITextViewEndEditing(NSNotification notification)
    {
        DumpNotification(notification);
        _editing = false;

        if (!_shown)
        {
            ResetTextViewData();
        }
    }

    private static void WillKeyboardShow(NSNotification notification)
    {
        DumpNotification(notification);
        _shown = true;

        var userInfo = notification.UserInfo;

        if (userInfo is not null)
        {
            AdjustOrReset(userInfo);
        }
    }

    private static void WillHideKeyboard(NSNotification notification)
    {
        DumpNotification(notification);
        _shown = false;

        var userInfo = notification.UserInfo;

        if (userInfo is not null)
        {
            AdjustOrReset(userInfo, true);
        }
    }

    private static void DidChangeFrame(NSNotification notification)
    {
        DumpNotification(notification);

        if (!_adjusted && notification.UserInfo is { } userInfo)
        {
            DumpInfo("ChangeFrame AdjustOrReset");
            AdjustOrReset(userInfo);
        }

        DumpInfo("ChangeFrame Skip");
        _adjusted = false;
    }

    private static UIWindow? GetApplicationWindow()
        => Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as UIWindow;

    private static void AdjustOrReset(NSDictionary userInfo, bool hiding = false)
    {
        userInfo.SetAnimationDuration();

        var startFrameSize = userInfo.GetValueOrDefault("UIKeyboardFrameBeginUserInfoKey");
        var endFrameSize = userInfo.GetValueOrDefault("UIKeyboardFrameEndUserInfoKey");

        // We need keyboard frames to be able to adjust the view.
        if (DescriptionToCGRect(startFrameSize?.Description) is not { } startFrameSizeRect ||
            DescriptionToCGRect(endFrameSize?.Description) is not { } endFrameSizeRect)
        {
            // If the start frame size is null, we can't adjust, so we just return
            DumpInfo("Keyboard frame size is null, skipping adjustment.");
            return;
        }

        // If the keyboard frame is not the full width of the screen,
        // it means the device is being rotated, so we can't trust this keyboard notification,
        // so skip and wait for the next one.
        if (endFrameSizeRect.Width != _screenWidth)
        {
            DumpInfo("Keyboard frame width does not match screen width, skipping adjustment.");
            return;
        }

        // To properly evaluate the keyboard height, we need to intersect the keyboard frame with the window frame.
        if ((_textView?.Window ?? GetApplicationWindow()) is { } window)
        {
            var newStartFrameSizeRect = CGRect.Intersect(startFrameSizeRect, window.Frame);
            var newEndFrameSizeRect = CGRect.Intersect(endFrameSizeRect, window.Frame);

            // When rotating the device from landscape left to landscape right, we get a non-final notification which we should skip
            if (!hiding && endFrameSizeRect.Height != 0 && newEndFrameSizeRect.Height == 0)
            {
                DumpInfo("Not hiding when keyboard frame height is zero, skipping adjustment.");
                return;
            }

            startFrameSizeRect = newStartFrameSizeRect;
            endFrameSizeRect = newEndFrameSizeRect;
        }

        // Sometimes `WillHideKeyboard` is called even when the keyboard will not hide, but simply change its frame.
        // The only way to determine if the keyboard is actually hiding is to check if the start and end frames are the same size.
        var willHide = hiding &&
                       startFrameSizeRect.Height == endFrameSizeRect.Height &&
                       startFrameSizeRect.Width == endFrameSizeRect.Width;

        if (willHide && !hiding)
        {
            // Sometimes `WillHideKeyboard` is called even when the keyboard will change to a different frame
            // so if the frame change is not matching the hiding behavior, we skip this notification.
            DumpInfo("Will hide keyboard, but the frames do not prove it, skipping adjustment.");
            return;
        }

        // If the keyboard height is very small (quick type bar) or if we are hiding the keyboard for real
        if (endFrameSizeRect.Height <= 80 || willHide)
        {
            Reset();
            DumpInfo($"Keyboard height is small {endFrameSizeRect.Height} or hiding {willHide}, resetting adjustments.");
            _adjusted = true;

            return;
        }

        _keyboardFrame = endFrameSizeRect;
        State.Height = _keyboardFrame.Height;
        State.IsVisible = true;
        
        DumpInfo($"Keyboard frame adjusted: {endFrameSizeRect} (visible: {State.IsVisible}, height: {State.Height})");

        Adjust();
        _adjusted = true;
    }

    private static void Reset()
    {
        if (!State.IsVisible)
        {
            DumpInfo("Keyboard is not visible, no need to reset adjustments.");
            return;
        }

        State.IsVisible = false;

        if (_containerView != null &&
            _rootView != null)
        {
            double offsetY = 0;
            var parentScrollView = FindParentVerticalScroll(_textView?.FindPlatformResponder<UIScrollView>());
            if (_adjustMode == SoftKeyboardAdjustMode.Resize &&
                parentScrollView is not null)
            {
                offsetY = parentScrollView.ContentOffset.Y;
            }

            void RestoreScrollView()
            {
                parentScrollView?.SetContentOffset(
                    new CGPoint(
                        parentScrollView.ContentOffset.X,
                        offsetY
                    ),
                    true
                );
            }

            UIView.Animate(
                _animationDuration,
                0,
                UIViewAnimationOptions.CurveEaseInOut,
                () =>
                {
                    RestoreRootView();
                    RestoreContainerView();
                    RestoreScrollView();
                },
                RestoreScrollView
            );
        }

        _panDelta = null;
        _resizeDelta = null;

        if (!_editing)
        {
            ResetTextViewData();
        }
    }

    private static void ResetTextViewData()
    {
        if (_textViewResizedTimerRunning)
        {
            _textViewResizedTimer!.Suspend();
            _textViewResizedTimerRunning = false;
        }

        MauiKeyboardScrollManagerHandlingFlag = false;

        if (_containerView is not null && _containerViewConstraints is not null)
        {
            _containerView.TranslatesAutoresizingMaskIntoConstraints = true;
            NSLayoutConstraint.DeactivateConstraints(_containerViewConstraints);
            _containerViewConstraints = null;
        }

        _containerView = null;
        _rootView = null;
        _textView = null;
        _adjustMode = DefaultAdjustMode;
    }

    private static void RestoreRootView()
    {
        DumpInfo($"Restoring root view {_rootView} frame from pan {_panDelta}.");
        
        if (_panDelta.HasValue && _rootView is not null)
        {
            var frame = _rootView.Frame;

            _rootView.Frame = new CGRect(
                frame.X,
                frame.Y - (nfloat) _panDelta.Value,
                frame.Width,
                frame.Height
            );

            _panDelta = null;
        }
    }

    private static void RestoreContainerView()
    {
        DumpInfo($"Restoring container view {_containerView} frame from resize {_resizeDelta}.");
        
        if (_containerViewConstraints is not null && _containerView is not null)
        {
            _containerViewConstraints[0].Constant = 0;
            _containerView.Superview?.SetNeedsLayout();
            _resizeDelta = null;
        } 
    }

    internal static void Adjust()
    {
        if (_textView?.Window is null ||
            _keyboardFrame == CGRect.Empty ||
            _containerViewConstraints is null ||
            _containerView is not { Window: { } window } ||
            _rootView is null)
        {
            // Can't adjust if we don't have the necessary views or keyboard frame
            MauiKeyboardScrollManagerHandlingFlag = false;

            return;
        }

        MauiKeyboardScrollManagerHandlingFlag = true;

        if (_adjustMode != SoftKeyboardAdjustMode.Resize && _resizeDelta.HasValue)
        {
            UIView.Animate(
                _animationDuration,
                0,
                UIViewAnimationOptions.CurveEaseInOut,
                RestoreContainerView,
                ForceAdjustAgain
            );

            return;
        }

        if (_adjustMode != SoftKeyboardAdjustMode.Pan && _panDelta.HasValue)
        {
            UIView.Animate(
                _animationDuration,
                0,
                UIViewAnimationOptions.CurveEaseInOut,
                RestoreRootView,
                ForceAdjustAgain
            );

            return;
        }

        if (_adjustMode == SoftKeyboardAdjustMode.None)
        {
            return;
        }

        var rootViewFrame = _rootView.Frame;

        var originalRootViewFrame = _panDelta.HasValue
            ? new CGRect(
                rootViewFrame.X,
                rootViewFrame.Y - (nfloat) _panDelta.Value,
                rootViewFrame.Width,
                rootViewFrame.Height
            )
            : rootViewFrame;

        CGRect newFrame;

        var originalContainerViewFrame = new CGRect(_containerView.Frame.X, _containerView.Frame.Y, _containerView.Frame.Width, _containerView.Frame.Height - (_resizeDelta ?? 0));
        var keyboardFrameInWindow = CGRect.Intersect(_keyboardFrame, window.Frame);
        var originalContainerViewFrameInWindow = _containerView.ConvertRectToView(originalContainerViewFrame, null);
        var keyboardIntersection = CGRect.Intersect(originalContainerViewFrameInWindow, keyboardFrameInWindow);

        if (_adjustMode == SoftKeyboardAdjustMode.Resize)
        {
            var resizeDelta = -keyboardIntersection.Height;

            _resizeDelta = resizeDelta;

            UIView.Animate(
                _animationDuration,
                0,
                UIViewAnimationOptions.CurveEaseInOut,
                () =>
                {
                    // If the previous mode was pan, we need to restore the root view frame
                    RestoreRootView();

                    _containerViewConstraints[0].Constant = resizeDelta;
                    _containerView.Superview?.LayoutIfNeeded();
                },
                ScrollToField
            );
        }
        else
        {
            var cursorPosition = FindCursorPosition() ?? CGRect.Empty;
            var cursorY = cursorPosition.Y + (originalRootViewFrame.Y - _rootView.Frame.Y);
            var cursorBottom = cursorY + cursorPosition.Height + _textViewDistanceFromBottom;
            var safeAreaBottom = _containerView.SafeAreaInsets.Bottom;
            var panY = cursorBottom > keyboardIntersection.Top ? keyboardIntersection.Top - cursorBottom - safeAreaBottom : 0;
            var maxPanValue = _keyboardFrame.Height;

            if (panY < -maxPanValue)
            {
                panY = -maxPanValue;
            }

            _panDelta = panY;

            newFrame = new CGRect(
                originalRootViewFrame.X,
                originalRootViewFrame.Y + panY,
                originalRootViewFrame.Width,
                originalRootViewFrame.Height
            );

            UIView.Animate(
                _animationDuration,
                0,
                UIViewAnimationOptions.CurveEaseInOut,
                () =>
                {
                    _rootView.Frame = newFrame;
                    // If the previous mode was resize, we need to restore the container view frame
                    _containerViewConstraints[0].Constant = 0;
                    _containerView.Superview?.LayoutIfNeeded();
                },
                Noop
            );
        }

        return;

        void ForceAdjustAgain()
        {
            Adjust();
        }

        void ScrollToField()
        {
            if (_textView?.Window is null)
            {
                return;
            }

            // Ensure that - if the text view is *inside* a UIScrollView - the cursor is fully visible
            var parentScrollView = FindParentVerticalScroll(_textView.FindPlatformResponder<UIScrollView>());

            if (parentScrollView is not null)
            {
                var cursorPosition = FindCursorPosition() ?? CGRect.Empty;
                var cursorY = cursorPosition.Y + (originalRootViewFrame.Y - _rootView!.Frame.Y);
                var cursorBottom = cursorY + cursorPosition.Height + _textViewDistanceFromBottom;

                if (cursorBottom > _keyboardFrame.Y)
                {
                    cursorPosition = FindLocalCursorPosition() ?? CGRect.Empty;
                    var cursorRect = _textView.ConvertRectToView(cursorPosition, parentScrollView);
                    cursorRect = new CGRect(cursorRect.X, cursorRect.Y, cursorRect.Width, cursorRect.Height + _textViewDistanceFromBottom);
                    MauiKeyboardScrollManagerHandlingFlag = false;
                    parentScrollView.ScrollRectToVisible(cursorRect, true);
                    MauiKeyboardScrollManagerHandlingFlag = true;
                }
            }
        }

        void Noop()
        {
            // No operation, just to satisfy the delegate signature
        }
    }

    // private static void PreFixContentOffset(CGRect originalRootViewFrame, CGRect keyboardIntersection)
    // {
    //     var parentScrollView = FindParentVerticalScroll(_textView!.FindPlatformResponder<UIScrollView>());
    //     if (parentScrollView is not null)
    //     {
    //         // Now check how much the cursor is hidden behind the keyboard
    //         var cursorPosition = FindCursorPosition() ?? CGRect.Empty;
    //         var cursorY = cursorPosition.Y + (originalRootViewFrame.Y - _rootView!.Frame.Y);
    //         var cursorBottom = cursorY + cursorPosition.Height + _textViewDistanceFromBottom;
    //         var safeAreaBottom = _containerView!.SafeAreaInsets.Bottom;
    //         var panY = cursorBottom > keyboardIntersection.Top
    //             ? keyboardIntersection.Top - cursorBottom - safeAreaBottom
    //             : 0;
    //
    //         if (panY != 0)
    //         {
    //             var currentOffset = parentScrollView.ContentOffset;
    //             var newOffset = Math.Max(0, currentOffset.Y - panY);
    //             parentScrollView.ContentOffset = new CGPoint(currentOffset.X, newOffset);
    //         }
    //     }
    // }

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

    private static UIScrollView? FindParentVerticalScroll(UIScrollView? view)
    {
        while (view is not null)
        {
            if (view.ScrollEnabled && view.Frame.Height < view.ContentSize.Height + view.AdjustedContentInset.Bottom + view.AdjustedContentInset.Top)
            {
                return view;
            }

            view = view.FindPlatformResponder<UIScrollView>();
        }

        return null;
    }

    private static CGRect? FindCursorPosition()
    {
        var localCursor = FindLocalCursorPosition();

        if (localCursor is { } local && _containerView is not null)
        {
            var cursorInContainer = _containerView.ConvertRectFromView(local, _textView);
            var cursorInWindow = _containerView.ConvertRectToView(cursorInContainer, null);

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
