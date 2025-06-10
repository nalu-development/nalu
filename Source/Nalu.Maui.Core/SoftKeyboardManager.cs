using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// Manager for handling soft keyboard adjustments.
/// </summary>
public static partial class SoftKeyboardManager
{
    /// <summary>
    /// The default soft keyboard adjustment mode to use when no specific mode is specified.
    /// </summary>
    public static SoftKeyboardAdjustMode DefaultAdjustMode { get; internal set; }
    
    /// <summary>
    /// Exposes the state of the soft keyboard.
    /// </summary>
    public static readonly SoftKeyboardState State = new();

#if IOS
    private static readonly ConditionalWeakTable<UIKit.UIView, StrongBox<SoftKeyboardAdjustMode>> _adjustRuleViews = [];
#endif

    /// <summary>
    /// Bindable property for setting the soft keyboard adjustment mode on a page.
    /// </summary>
    public static readonly BindableProperty SoftKeyboardAdjustModeProperty =
        BindableProperty.CreateAttached(
            "SoftKeyboardAdjustMode",
            typeof(SoftKeyboardAdjustMode?),
            typeof(SoftKeyboardManager),
            null,
            propertyChanged: OnSoftKeyboardAdjustModeChanged
        );
    
    /// <summary>
    /// Sets the soft keyboard adjustment mode for a page.
    /// </summary>
    public static void SetSoftKeyboardAdjustMode(BindableObject bindable, SoftKeyboardAdjustMode? value)
        => bindable.SetValue(SoftKeyboardAdjustModeProperty, value);
    
    /// <summary>
    /// Gets the soft keyboard adjustment mode for a page.
    /// </summary>
    public static SoftKeyboardAdjustMode? GetSoftKeyboardAdjustMode(BindableObject bindable)
        => (SoftKeyboardAdjustMode?)bindable.GetValue(SoftKeyboardAdjustModeProperty);

    private static void OnSoftKeyboardAdjustModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = bindable as VisualElement ?? throw new InvalidCastException("bindable must be a VisualElement");

        view.HandlerChanging -= ViewOnHandlerChanging;
        view.HandlerChanged -= ViewOnHandlerChanged;

        view.HandlerChanging += ViewOnHandlerChanging;
        view.HandlerChanged += ViewOnHandlerChanged;

        if (view.Handler is not null)
        {
            ViewOnHandlerChanged(bindable, EventArgs.Empty);
        }
    }

    private static void ViewOnHandlerChanged(object? sender, EventArgs e)
    {
        var view = (VisualElement) sender!;
        if (view.Handler is { PlatformView: not null } handler)
        {
            if (GetSoftKeyboardAdjustMode(view) is { } adjustMode)
            {
                AddPlatformViewAdjustModeRef(handler, adjustMode);
            }
            else
            {
                RemovePlatformViewAdjustModeRef(handler);
            }
        }
    }

    private static void AddPlatformViewAdjustModeRef(IViewHandler handler, SoftKeyboardAdjustMode adjustMode)
    {
#if ANDROID
        if (handler is { PlatformView: Android.Views.View newPlatformView })
        {
            // ReSharper disable once AccessToStaticMemberViaDerivedType
            newPlatformView.SetTag(Resource.Id.nalu_soft_keyboard_adjust_mode_tag_key, (int)adjustMode);
        }
#endif
#if IOS
        if (handler is { PlatformView: UIKit.UIView newPlatformView })
        {
            _adjustRuleViews.AddOrUpdate(newPlatformView, new StrongBox<SoftKeyboardAdjustMode>(adjustMode));
        }
#endif
    }

    private static void ViewOnHandlerChanging(object? sender, HandlerChangingEventArgs e)
    {
        var handler = e.OldHandler;

        RemovePlatformViewAdjustModeRef(handler);
    }

    private static void RemovePlatformViewAdjustModeRef(IElementHandler handler)
    {
#if ANDROID
        if (handler is { PlatformView: Android.Views.View oldPlatformView })
        {
            oldPlatformView.SetTag(Resource.Id.nalu_soft_keyboard_adjust_mode_tag_key, null);
        }
#endif
#if IOS
        if (handler is { PlatformView: UIKit.UIView oldPlatformView })
        {
            _adjustRuleViews.Remove(oldPlatformView);
        }
#endif
    }
}

/// <summary>
/// Enumeration for soft keyboard adjustment modes.
/// </summary>
public enum SoftKeyboardAdjustMode
{
    /// <summary>
    /// No adjustment is made when the soft keyboard appears.
    /// </summary>
    None,

    /// <summary>
    /// Adjusts the layout by resizing the content when the soft keyboard appears.
    /// </summary>
    Resize,
    
    /// <summary>
    /// Adjusts the layout by panning the content when the soft keyboard appears.
    /// </summary>
    Pan,
}
