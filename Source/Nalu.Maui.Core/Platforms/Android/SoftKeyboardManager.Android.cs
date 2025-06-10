using Android.Views;
using AndroidX.Core.View;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
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
                        treeObserver.RemoveOnGlobalFocusChangeListener(softKeyboardAdjustModeFocusListener);
                        ViewCompat.SetOnApplyWindowInsetsListener(rootView, null);
                    };
                }
            }
        );
    
    
#pragma warning disable VSTHRD100
        private static async void ScrollToFocusedField(View focusedView)
#pragma warning restore VSTHRD100
        {
            await Task.Delay(10); // Allow time for the layout to adjust
            focusedView.RequestRectangleOnScreen(new Rect(0, 0, focusedView.Width, focusedView.Height), false);
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
            var tag = textView.GetTag(Resource.Id.nalu_soft_keyboard_adjust_mode_tag_key);

            if (tag is Java.Lang.Integer integer)
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
    
    private class OnApplyWindowInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(View v, WindowInsetsCompat insets)
        {
            var keyboardType = WindowInsetsCompat.Type.Ime();

            var wasVisible = State.IsVisible;
            State.IsVisible = insets.IsVisible(keyboardType);

            var keyboardHeight = insets.GetInsets(keyboardType).Bottom;
            State.Height = v.Context.FromPixels(keyboardHeight);

            if (!wasVisible && State.IsVisible && _focusedView?.TryGetTarget(out var focusedView) is true)
            {
                SetAdjustMode(focusedView);
                ScrollToFocusedField(focusedView);
            }

            return insets;
        }
    }

    private class SoftKeyboardAdjustModeFocusListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalFocusChangeListener
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
