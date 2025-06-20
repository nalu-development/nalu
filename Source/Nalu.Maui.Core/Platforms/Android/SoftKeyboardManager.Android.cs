using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Java.Lang;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using Object = Java.Lang.Object;
using Rect = Android.Graphics.Rect;
using View = Android.Views.View;
using Window = Android.Views.Window;

// ReSharper disable InconsistentNaming

namespace Nalu;

/// <summary>
/// Manager for handling soft keyboard adjustments.
/// </summary>
public static partial class SoftKeyboardManager
{
    private static Window? _window;
    private static Action? _unsubscribe;
    private static WeakReference<View>? _focusedView;

    internal static void Configure(MauiAppBuilder builder)
        => builder.ConfigureLifecycleEvents(events => events.AddAndroid(Configure));

    private static void Configure(this IAndroidLifecycleBuilder lifecycle)
        => lifecycle.OnCreate((activity, _) =>
            {
                if (activity.Window is { DecorView.RootView: { ViewTreeObserver: { } treeObserver } rootView } window)
                {
                    _unsubscribe?.Invoke();

                    _window = window;

                    var softKeyboardAdjustModeFocusListener = new SoftKeyboardAdjustModeFocusListener();
                    var onApplyWindowInsetsListener = new OnApplyWindowInsetsListener();

                    ViewCompat.SetOnApplyWindowInsetsListener(rootView, onApplyWindowInsetsListener);
                    treeObserver!.AddOnGlobalFocusChangeListener(softKeyboardAdjustModeFocusListener);

                    _unsubscribe = () =>
                    {
                        var currentRootView = window.DecorView.RootView;
                        var currentTreeObserver = currentRootView?.ViewTreeObserver;

                        if (currentTreeObserver?.IsAlive == true)
                        {
                            currentTreeObserver.RemoveOnGlobalFocusChangeListener(softKeyboardAdjustModeFocusListener);
                        }

                        ViewCompat.SetOnApplyWindowInsetsListener(rootView, null);
                    };
                }
            }
        );

#pragma warning disable VSTHRD100
    private static async void ScrollToFocusedField(View focusedView)
#pragma warning restore VSTHRD100
    {
        await Task.Delay(50); // Allow time for the layout to adjust

        (int Y1, int Y2) cursorCoordinates = (0, focusedView.Height);

        if (focusedView is EditText editText)
        {
            cursorCoordinates = GetSelectionCoordinates(editText);
        }

        var marginBottom = (int) focusedView.Context.ToPixels(20);
        focusedView.RequestRectangleOnScreen(new Rect(0, cursorCoordinates.Y1, focusedView.Width, cursorCoordinates.Y2 + marginBottom), false);
    }

    private static (int Y1, int Y2) GetSelectionCoordinates(EditText editText)
    {
        var start = editText.SelectionStart;
        var end = editText.SelectionEnd;
        var layout = editText.Layout;

        if (layout == null)
        {
            return (0, editText.Height);
        }

        var startLine = layout.GetLineForOffset(start);
        var y1 = layout.GetLineBaseline(startLine) + (int) layout.Paint.Ascent();

        var endLine = layout.GetLineForOffset(end);
        var y2 = layout.GetLineBaseline(endLine) + (int) layout.Paint.Descent();

        return (y1, y2);
    }

    private static void SetAdjustMode(View textView)
    {
        // ReSharper disable once MergeAndPattern
        // ReSharper disable once ConvertTypeCheckPatternToNullCheck
        if (TryGetAdjustMode(textView, out var adjustMode))
        {
            SetWindowSoftInputMode(adjustMode);

            return;
        }

        var parent = textView.Parent;

        while (parent is not null)
        {
            if (parent is View parentView &&
                TryGetAdjustMode(parentView, out var parentAdjustMode))
            {
                SetWindowSoftInputMode(parentAdjustMode);

                return;
            }

            parent = parent.Parent;
        }

        SetWindowSoftInputMode(DefaultAdjustMode);
    }

    private static bool TryGetAdjustMode(View textView, out SoftKeyboardAdjustMode adjustMode)
    {
        // ReSharper disable once MergeAndPattern
        // ReSharper disable once ConvertTypeCheckPatternToNullCheck
        var key = AppIds.GetId("nalu_soft_keyboard_adjust_mode_tag_key", textView);
        var tag = textView.GetTag(key);

        if (tag is Integer integer)
        {
            adjustMode = (SoftKeyboardAdjustMode) integer.IntValue();

            return true;
        }

        adjustMode = default;

        return false;
    }

    private static void SetWindowSoftInputMode(SoftKeyboardAdjustMode adjustMode)
    {
        State.AdjustMode = adjustMode;

        _window?.SetSoftInputMode(
            adjustMode switch
            {
                SoftKeyboardAdjustMode.Pan => SoftInput.AdjustPan,
                SoftKeyboardAdjustMode.Resize => SoftInput.AdjustResize,
                _ => SoftInput.AdjustNothing
            }
        );
    }

    private class OnApplyWindowInsetsListener : Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            var keyboardType = WindowInsetsCompat.Type.Ime();

            var wasVisible = State.IsVisible;
            var isVisible = insets.IsVisible(keyboardType);

            if (!wasVisible && isVisible && _focusedView?.TryGetTarget(out var focusedView) is true)
            {
                SetAdjustMode(focusedView);
                ScrollToFocusedField(focusedView);
            }

            var keyboardHeight = insets.GetInsets(keyboardType).Bottom;
            State.Height = v.Context.FromPixels(keyboardHeight);

            State.IsVisible = isVisible;

            return insets;
        }
    }

    private class SoftKeyboardAdjustModeFocusListener : Object, ViewTreeObserver.IOnGlobalFocusChangeListener
    {
        public void OnGlobalFocusChanged(View? oldFocus, View? newFocus)
        {
            _focusedView = newFocus != null ? new WeakReference<View>(newFocus) : null;

            if (State.IsVisible && newFocus is not null)
            {
                SetAdjustMode(newFocus);
                ScrollToFocusedField(newFocus);
            }
        }
    }
}
