using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Gives an overlay host its OWN MAUI window-insets listener for the MAUI content below it
/// (<c>MauiWindowInsetListener.RegisterParentForChildViews</c>, internal in MAUI 10).
/// </summary>
/// <remarks>
/// <para>
/// MAUI's per-view inset listeners resolve to the nearest REGISTERED ancestor's listener; without
/// this, the overlay content shares the window root's instance, which (1) gates every dispatch
/// while an IME animation runs (padding lands only at the end — the keyboard "jump") and (2)
/// applies its not-yet-laid-out heuristic against the ROOT's tracked views (a freshly mounted sheet
/// gets no bottom padding until something else re-dispatches). A dedicated instance never hears
/// the IME animation callbacks (they stop at the root) and has no tracked views of its own, so
/// dispatches through the host apply immediately and completely.
/// </para>
/// <para>
/// Reached by reflection, trim/AOT-safe: the member is rooted with <see cref="DynamicDependencyAttribute"/>
/// and the type name is a compile-time constant. Absent member (a future MAUI) → graceful no-op:
/// the overlays fall back to the shared listener.
/// </para>
/// </remarks>
internal static class ScaffoldMauiInsetListenerBridge
{
    private const string _listenerTypeName = "Microsoft.Maui.Platform.MauiWindowInsetListener, Microsoft.Maui";
    private const string _registerMethodName = "RegisterParentForChildViews";
    private const string _unregisterMethodName = "UnregisterView";

    private static readonly MethodInfo? _register = Resolve(_registerMethodName);
    private static readonly MethodInfo? _unregister = Resolve(_unregisterMethodName);

    [DynamicDependency(_registerMethodName, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
    [DynamicDependency(_unregisterMethodName, "Microsoft.Maui.Platform.MauiWindowInsetListener", "Microsoft.Maui")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:UnrecognizedReflectionPattern", Justification = "The members are rooted through DynamicDependency.")]
    private static MethodInfo? Resolve(string name)
    {
        var type = Type.GetType(_listenerTypeName, throwOnError: false);

        return type?.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
    }

    /// <summary>Registers the host as the inset-listener parent of its MAUI descendants (call BEFORE they attach).</summary>
    public static void RegisterParent(AView host)
    {
        try
        {
            _register?.Invoke(null, [host, null]);
        }
        catch (TargetInvocationException)
        {
            // Fall back to the shared listener.
        }
    }

    /// <summary>
    /// Forgets the host's registration (and its listener, whose tracked-views set would otherwise keep
    /// the host's last MAUI content alive through MAUI's static registry — a page leak).
    /// </summary>
    public static void UnregisterParent(AView host)
    {
        try
        {
            _unregister?.Invoke(null, [host]);
        }
        catch (TargetInvocationException)
        {
        }
    }
}
