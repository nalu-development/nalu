using System.Globalization;
using System.Reflection;
using System.Text;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using UIKit;
using ContentView = Microsoft.Maui.Platform.ContentView;

// ReSharper disable InconsistentNaming

namespace Nalu;

/// <summary>
/// Manager for handling soft keyboard adjustments.
/// </summary>
public static partial class SoftKeyboardManager
{

    private static readonly FieldInfo? _isKeyboardAutoScrollHandlingField
        = typeof(KeyboardAutoManagerScroll).GetField("IsKeyboardAutoScrollHandling", BindingFlags.NonPublic);

    private const int _textViewDistanceFromBottom = 20;
    private static NSObject? _willShowToken;
    private static NSObject? _willHideToken;
    private static NSObject? _textFieldToken;
    private static NSObject? _textFieldEndToken;
    private static NSObject? _textViewToken;
    private static NSObject? _textViewEndToken;
    private static NSObject? _orientationChangeToken;
    private static NSObject? _didChangeFrameToken;
    private static IDisposable? _textChanged;
    private static UIView? _textView;
    private static UIView? _rootView;
    private static UIView? _containerView;
    private static double? _containerViewWidth;
    private static double? _resizeDelta;
    private static double? _panDelta;
    private static SoftKeyboardAdjustMode _adjustMode = SoftKeyboardAdjustMode.Resize;
    private static double _animationDuration = 0.25;
    private static CGRect _keyboardFrame;
    private static double _screenWidth;
    private static bool _adjusted;

    private static bool MauiKeyboardScrollManagerHandlingFlag {
        get => _isKeyboardAutoScrollHandlingField?.GetValue(null) as bool? ?? false;
        set => _ = value; // _isKeyboardAutoScrollHandlingField?.SetValue(null, value);
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
    }

    private static void OrientationChanged(NSNotification obj)
    {
        var screenSize = UIScreen.MainScreen.Bounds.Size;
        var orientation = UIDevice.CurrentDevice.Orientation;

        var width = orientation switch
        {
            UIDeviceOrientation.LandscapeLeft or UIDeviceOrientation.LandscapeRight => Math.Max(screenSize.Height, screenSize.Width),
            UIDeviceOrientation.Portrait or UIDeviceOrientation.PortraitUpsideDown => Math.Min(screenSize.Height, screenSize.Width),
            _ => screenSize.Width
        };
        
        _screenWidth = width;
    }

    private static void DidUITextViewEndEditing(NSNotification notification)
    {
        if (_textChanged is not null)
        {
            _textChanged.Dispose();
            _textChanged = null;
        }
    }

    private static void WillKeyboardShow(NSNotification notification)
    {
        var userInfo = notification.UserInfo;

        if (userInfo is not null)
        {
            AdjustOrReset(userInfo);
        }
    }

    private static void WillHideKeyboard(NSNotification notification)
    {
        var userInfo = notification.UserInfo;

        if (userInfo is not null)
        {
            AdjustOrReset(userInfo, true);
        }
    }

    private static void DidChangeFrame(NSNotification notification)
    {
        if (_textView is null)
        {
            MauiKeyboardScrollManagerHandlingFlag = false;
        }

        if (!_adjusted && notification.UserInfo is { } userInfo)
        {
            AdjustOrReset(userInfo);
        }
        
        _adjusted = false;
    }

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
            return;
        }
        
        // If the keyboard frame is not the full width of the screen,
        // it means the device is being rotated, so we can't trust this keyboard notification,
        // so skip and wait for the next one.
        if (endFrameSizeRect.Width != _screenWidth)
        {
            return;
        }

        // To properly evaluate the keyboard height, we need to intersect the keyboard frame with the window frame.
        if (_textView?.Window is { } window)
        {
            var newStartFrameSizeRect = CGRect.Intersect(startFrameSizeRect, window.Frame);
            var newEndFrameSizeRect = CGRect.Intersect(endFrameSizeRect, window.Frame);

            // When rotating the device from landscape left to landscape right, we get a non-final notification which we should skip
            if (endFrameSizeRect.Height != 0 && newEndFrameSizeRect.Height == 0)
            {
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
            return;
        }

        // If the keyboard height is very small (quick type bar) or if we are hiding the keyboard for real
        if (endFrameSizeRect.Height <= 80 || willHide)
        {
            Reset();
            _adjusted = true;

            return;
        }

        _keyboardFrame = endFrameSizeRect;
        State.Height = _keyboardFrame.Height;
        State.IsVisible = true;

        Adjust();
        _adjusted = true;
    }

    private static void Reset()
    {
        if (!State.IsVisible)
        {
            return;
        }

        State.IsVisible = false;

        if (_containerView != null &&
            _rootView != null)
        {
            UIView.Animate(_animationDuration, 0, UIViewAnimationOptions.CurveEaseInOut,
                           () =>
                           {
                               RestoreRootView();
                               RestoreContainerView();
                           }, () => {});
        }
        
        _panDelta = null;
        _resizeDelta = null;
        _containerViewWidth = null;
        _containerView = null;
        _rootView = null;
        _textView = null;
        _adjustMode = DefaultAdjustMode;
    }

    private static void RestoreRootView()
    {
        if (_panDelta.HasValue && _rootView is not null)
        {
            var frame = _rootView.Frame;
            _rootView.Frame = new CGRect(
                frame.X,
                frame.Y - (nfloat)_panDelta.Value,
                frame.Width,
                frame.Height
            );
            
            _panDelta = null;
        }
    }

    private static void RestoreContainerView()
    {
        if (_resizeDelta.HasValue && _containerView is not null)
        {
            var frame = _containerView.Frame;
            _containerView.Frame = new CGRect(
                frame.X,
                frame.Y,
                frame.Width,
                frame.Height - (nfloat)_resizeDelta.Value
            );
            
            _resizeDelta = null;
            _containerViewWidth = null;
        }
    }

    private static void Adjust()
    {
        if (_textView is null ||
            _keyboardFrame == CGRect.Empty ||
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

        var containerViewFrame = _containerView.Frame;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        var originalContainerViewFrame = _containerViewWidth == containerViewFrame.Width && _resizeDelta.HasValue
            ? new CGRect(
                containerViewFrame.X,
                containerViewFrame.Y,
                containerViewFrame.Width,
                containerViewFrame.Height - (nfloat)_resizeDelta.Value
            )
            : containerViewFrame;

        _containerViewWidth = containerViewFrame.Width;
        
        var rootViewFrame = _rootView.Frame;
        var originalRootViewFrame = _panDelta.HasValue
            ? new CGRect(
                rootViewFrame.X,
                rootViewFrame.Y - (nfloat)_panDelta.Value,
                rootViewFrame.Width,
                rootViewFrame.Height
            )
            : rootViewFrame;

        CGRect newFrame;
        
        var keyboardFrameInWindow = CGRect.Intersect(_keyboardFrame, window.Frame);
        var originalContainerViewFrameInWindow = _containerView.ConvertRectToView(originalContainerViewFrame, null);
        var keyboardIntersection = CGRect.Intersect(originalContainerViewFrameInWindow, keyboardFrameInWindow);

        if (_adjustMode == SoftKeyboardAdjustMode.Resize)
        {
            newFrame = new CGRect(
                originalContainerViewFrame.X,
                originalContainerViewFrame.Y,
                originalContainerViewFrame.Width,
                originalContainerViewFrame.Height - keyboardIntersection.Height
            );

            var resizeDelta = -keyboardIntersection.Height;

            _resizeDelta = resizeDelta;

            UIView.Animate(
                _animationDuration,
                0,
                UIViewAnimationOptions.CurveEaseInOut,
                () =>
                {
                    _rootView.Frame = originalRootViewFrame;
                    _containerView.Frame = newFrame;
                    // If the previous mode was pan, we need to restore the root view frame
                    RestoreRootView();
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
                    _containerView.Frame = originalContainerViewFrame;
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
            // Ensure that - if the text view is *inside* a UIScrollView - the cursor is fully visible
            var parentScrollView = FindParentVerticalScroll(_textView!.FindPlatformResponder<UIScrollView>());

            if (parentScrollView is not null)
            {
                var cursorPosition = FindCursorPosition() ?? CGRect.Empty;
                var cursorY = cursorPosition.Y + (originalRootViewFrame.Y - _rootView!.Frame.Y);
                var cursorBottom = cursorY + cursorPosition.Height + _textViewDistanceFromBottom;

                if (cursorBottom > _keyboardFrame.Y)
                {
                    cursorPosition = FindLocalCursorPosition() ?? CGRect.Empty;
                    var cursorRect = _textView!.ConvertRectToView(cursorPosition, parentScrollView);
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

    private static void DidUITextBeginEditing(NSNotification notification)
    {
        if (notification.Object is UIView view)
        {
            _textView = view;

            if (_textView is UITextView textView)
            {
                // Only Layer is observable for UITextView.Frame changes
                _textChanged = UITextViewBoundsObserver.Attach(textView);
            }
            
            var parent = view;
            var containerView = view.GetContainerPlatformView();

            // When switching to a new container view with the keyboard already visible and the adjust mode was Resize,
            // we need to restore the previous container view's frame.
            if (_adjustMode == SoftKeyboardAdjustMode.Resize && !ReferenceEquals(containerView, _containerView))
            {
                RestoreContainerView();
            }

            _containerView = containerView;
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
        var durationNum = (NSNumber)NSObject.FromObject(durationObj);
        var num = (double)durationNum;
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
    
    internal static T? FindTopController<T>(this UIView view) where T : UIViewController
    {
        var bestController = view.FindPlatformResponder<T>();
        var tempController = bestController;

        while (tempController is not null)
        {
            tempController = tempController.View?.FindPlatformResponder<T>();

            if (tempController is not null)
            {
                bestController = tempController;
            }
        }

        return bestController;
    }
    
    private static T? FindPlatformResponder<T>(this UIView view) where T : UIResponder
    {
        var nextResponder = view as UIResponder;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        while (nextResponder is not null)
        {
            nextResponder = nextResponder.NextResponder;

            if (nextResponder is T responder)
            {
                return responder;
            }
        }
        return null;
    }

    [Register("NaluUITextViewBoundsObserver")]
    private class UITextViewBoundsObserver : NSObject
    {
        private static readonly NSString _boundsKey = new("bounds");

        private readonly WeakReference<CALayer> _layerReference;
        private bool _disposed;

        public static UITextViewBoundsObserver Attach(UITextView textView)
        {
            var layer = textView.Layer;
            _ = layer ?? throw new ArgumentNullException(nameof(layer));

            var observer = new UITextViewBoundsObserver(layer);
            layer.AddObserver(observer, _boundsKey, NSKeyValueObservingOptions.New, observer.Handle);
            return observer;
        }

        private UITextViewBoundsObserver(CALayer layer)
        {
            _layerReference = new WeakReference<CALayer>(layer);
            IsDirectBinding = false;
        }

        [Microsoft.Maui.Controls.Internals.Preserve(Conditional = true)]
        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            if (!_disposed && keyPath == _boundsKey && context == Handle && _layerReference.TryGetTarget(out _))
            {
                Adjust();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_layerReference.TryGetTarget(out var layer))
                {
                    layer?.RemoveObserver(this, _boundsKey);
                }
            }

            base.Dispose(disposing);
        }
    }
}
