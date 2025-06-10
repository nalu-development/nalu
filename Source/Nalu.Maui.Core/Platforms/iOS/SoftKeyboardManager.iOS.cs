using System.Reflection;
using System.Text;
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
    private static NSObject? _textViewToken;
    private static NSObject? _textViewEndToken;
    private static NSObject? _didChangeFrameToken;
    private static IDisposable? _textChanged;
    private static UIView? _textView;
    private static UIView? _rootView;
    private static UIView? _containerView;
    private static double? _resizeDelta;
    private static double? _panDelta;
    private static CGRect? _textViewFrame;
    private static SoftKeyboardAdjustMode _adjustMode = SoftKeyboardAdjustMode.Resize;
    private static double _animationDuration = 0.25;
    private static CGRect _keyboardFrame;

    private static bool MauiKeyboardScrollManagerHandlingFlag {
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
        _textViewToken = NSNotificationCenter.DefaultCenter.AddObserver(UITextView.TextDidBeginEditingNotification, DidUITextBeginEditing);
        _textViewEndToken = NSNotificationCenter.DefaultCenter.AddObserver(UITextView.TextDidEndEditingNotification, DidUITextViewEndEditing);
        _willShowToken = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillShowNotification, WillKeyboardShow);
        _willHideToken = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.WillHideNotification, WillHideKeyboard);
        _didChangeFrameToken = NSNotificationCenter.DefaultCenter.AddObserver(UIKeyboard.DidChangeFrameNotification, DidChangeFrame);
    }

    private static void DidUITextViewEndEditing(NSNotification notification)
    {
        if (_textChanged is not null)
        {
            _textChanged.Dispose();
            _textChanged = null;
            _textViewFrame = null;
        }
    }

    private static void DidChangeFrame(NSNotification notification)
    {
        if (_textView is null)
        {
            MauiKeyboardScrollManagerHandlingFlag = false;
        }
    }

    private static void WillHideKeyboard(NSNotification notification)
    {
        State.IsVisible = false;

        var userInfo = notification.UserInfo;
        userInfo?.SetAnimationDuration();

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
        _containerView = null;
        _rootView = null;
        _textView = null;
        _adjustMode = SoftKeyboardAdjustMode.Resize;
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
        }
    }

    private static void WillKeyboardShow(NSNotification notification)
    {
        var wasVisible = State.IsVisible;
        State.IsVisible = true;

        var userInfo = notification.UserInfo;

        if (userInfo is not null)
        {
            var frameSize = userInfo.GetValueOrDefault("UIKeyboardFrameEndUserInfoKey");
            var frameSizeRect = DescriptionToCGRect(frameSize?.Description);
            if (frameSizeRect is not null)
            {
                _keyboardFrame = (CGRect)frameSizeRect;
                State.Height = _keyboardFrame.Height;
            }

            userInfo.SetAnimationDuration();
        }
        else
        {
            _keyboardFrame = CGRect.Empty;
            State.Height = 0;
        }

        Adjust(!wasVisible);
    }

    private static void Adjust(bool again = false)
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
        
        if (_adjustMode == SoftKeyboardAdjustMode.Pan && _resizeDelta.HasValue)
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
        
        if (_adjustMode == SoftKeyboardAdjustMode.Resize && _panDelta.HasValue)
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

        var containerViewFrame = _containerView.Frame;
        var originalContainerViewFrame = _resizeDelta.HasValue
            ? new CGRect(
                containerViewFrame.X,
                containerViewFrame.Y,
                containerViewFrame.Width,
                containerViewFrame.Height - (nfloat)_resizeDelta.Value
            )
            : containerViewFrame;
        
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

            _resizeDelta = -keyboardIntersection.Height;

            UIView.Animate(_animationDuration, 0, UIViewAnimationOptions.CurveEaseInOut,
                           () =>
                           {
                               _rootView.Frame = originalRootViewFrame;
                               _containerView.Frame = newFrame;
                               // If the previous mode was pan, we need to restore the root view frame
                               RestoreRootView();
                           },
                           AdjustAgain);
        }
        else
        {
            var cursorPosition = FindCursorPosition() ?? CGRect.Empty;
            var cursorY = cursorPosition.Y + (originalRootViewFrame.Y - _rootView.Frame.Y);
            var cursorBottom = cursorY + cursorPosition.Height + _textViewDistanceFromBottom;
            var panY = cursorBottom > keyboardIntersection.Top ? keyboardIntersection.Top - cursorBottom : 0;
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
            
            UIView.Animate(_animationDuration, 0, UIViewAnimationOptions.CurveEaseInOut,
                           () =>
                           {
                               _rootView.Frame = newFrame;
                               _containerView.Frame = originalContainerViewFrame;
                           }, AdjustAgain);
        }

        return;
        
        void AdjustAgain()
        {
            if (again)
            {
                Adjust();
            }
        }
        
        void ForceAdjustAgain()
        {
            Adjust(true);
        }
    }

    private static void DidUITextBeginEditing(NSNotification notification)
    {
        if (notification.Object is UIView view)
        {
            _textView = view;

            if (_textView is UITextView)
            {
                // Only Layer is observable for UITextView.Frame changes
                _textChanged = _textView.Layer.AddObserver("bounds", NSKeyValueObservingOptions.New,
                                                      _ =>
                                                      {
                                                          Adjust();
                                                      });
                _textViewFrame = _textView.Frame;
            }
            
            var parent = view;

            while (parent != null)
            {
                if (_adjustRuleViews.TryGetValue(parent, out var adjustModeBox))
                {
                    _adjustMode = adjustModeBox.Value;
                    State.AdjustMode = _adjustMode;

                    if (_containerView is null)
                    {
                        _containerView = view.GetContainerPlatformView();
                        _rootView = _containerView?.Window.RootViewController?.View;
                    }

                    break;
                }
                
                parent = parent.Superview;
            }
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
            && nfloat.TryParse(dimensions[0], out var x)
            && nfloat.TryParse(dimensions[1], out var y)
            && nfloat.TryParse(dimensions[2], out var width)
            && nfloat.TryParse(dimensions[3], out var height))
        {
            return new CGRect(x, y, width, height);
        }

        return null;

        static string RemoveEverythingExceptForNumbersAndCommas(string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (var character in input)
            {
                if (char.IsDigit(character) || character == ',')
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
}
