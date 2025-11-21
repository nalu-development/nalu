using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using ViewExtensions = Microsoft.Maui.Controls.ViewExtensions;

#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

#if !NET9_0_OR_GREATER
using System.Reflection;
#endif

namespace Nalu.Internals;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class DisconnectHandlerHelper
{
#if !NET9_0_OR_GREATER
    private static readonly BindableProperty _disconnectPolicyProperty;

    static DisconnectHandlerHelper()
    {
        var policyBindableProperty =
            typeof(Button).Assembly.GetType("Microsoft.Maui.Controls.HandlerProperties")?.GetField("DisconnectPolicyProperty", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as BindableProperty;

        _disconnectPolicyProperty = policyBindableProperty ?? HandlerPropertiesBackport.DisconnectPolicyBackportProperty;
    }
#endif
    
    // .NET10 https://github.com/dotnet/runtime/pull/114881
    // [UnsafeAccessor("Microsoft.Maui.Platform.ViewExtensions", UnsafeAccessorKind.StaticMethod, Name = "OnUnloaded")]
    // internal static IDisposable OnUnloaded(IElement element, Action action)

    // .NET9 we have to use reflection
    private static readonly Func<IElement, Action, IDisposable>? _onUnloaded =
        typeof(Microsoft.Maui.Platform.ViewExtensions).GetMethod(
            "OnUnloaded",
            BindingFlags.Static | BindingFlags.NonPublic, [typeof(IElement), typeof(Action)]
        )?.CreateDelegate<Func<IElement, Action, IDisposable>>();

    public static void DisconnectHandlers(IView view)
    {
        if (view is VisualElement { IsLoaded: true } && _onUnloaded is not null)
        {
            _onUnloaded(view, () =>
            {
                SafeDisconnectHandlers(view);
            });
        }
        else
        {
            SafeDisconnectHandlers(view);
        }
    }

    private static void SafeDisconnectHandlers(IView view)
    {
        try
        {
            view.DisconnectHandlers();
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as needed
            Application.Current?.GetLogger<IViewHandler>()
                       ?.LogError(ex, "Error disconnecting handlers for view: {ViewType}", view.GetType().Name);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "StaticId")]
    public static extern ref Guid GetUnsafeStaticField();
}
