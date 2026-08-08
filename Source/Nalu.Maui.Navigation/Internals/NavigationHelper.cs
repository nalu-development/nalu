using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Nalu;

internal static partial class NavigationHelper
{
    /// <summary>
    /// Resolves the page's lifecycle target: the ONE object receiving navigation lifecycle
    /// callbacks (<see cref="IEnteringAware"/>, <see cref="IAppearingAware"/>,
    /// <see cref="ILeavingGuard"/>, intent methods…). An EXPLICITLY assigned
    /// <see cref="BindableObject.BindingContext"/> (the page model) wins entirely — when both
    /// the page and its binding context implement lifecycle interfaces, only the binding
    /// context is called. Without one, the page itself is the target (view-only pages
    /// implement the interfaces directly). An INHERITED binding context (MAUI parent
    /// propagation — hosted pages inherit from their Shell/Scaffold) is stored outside the
    /// property store and never counts: it is application state, not the page's model.
    /// </summary>
    /// <remarks>
    /// O(1), allocation-free (a property-context lookup). Deliberately NOT cached: the
    /// binding context may legally be assigned after creation (e.g. inside
    /// <c>OnEnteringAsync</c>), which must flip the target on the next dispatch.
    /// </remarks>
    public static object GetLifecycleTarget(Page page)
        => page.IsSet(BindableObject.BindingContextProperty) ? page.BindingContext ?? page : page;

    private static readonly AsyncLocal<Page?> _lifecyclePage = new();

    /// <summary>
    /// The page whose lifecycle callback is currently executing, flowed via
    /// <see cref="AsyncLocal{T}"/> so it is visible INSIDE the callback's async flow even when
    /// the page is not on the committed stack yet (multi-push batches commit at the end) —
    /// the seam <see cref="NavigationRestoreService"/> uses to deduce the current page.
    /// </summary>
    internal static Page? AmbientLifecyclePage => _lifecyclePage.Value;

    /// <summary>
    /// Invokes a lifecycle callback with <see cref="AmbientLifecyclePage"/> set: the async
    /// wrapper's execution context is a copy, so the value flows into the callback (and
    /// anything it awaits) without leaking to the engine's context.
    /// </summary>
    private static async ValueTask WithLifecyclePageAsync(Page page, Func<ValueTask> invoke)
    {
        _lifecyclePage.Value = page;
        await invoke();
    }

    private static MethodInfo? GetImplementedLifecycleMethod(
        Regex methodRegex, 
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type targetType,
        Type intentType)
    {
        var methodInfo = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                   .Where(m => methodRegex.IsMatch(m.Name) && m.ReturnType == typeof(ValueTask))
                                   .FirstOrDefault(m =>
                                       {
                                           var parameters = m.GetParameters();
                                           return parameters.Length == 1 && intentType.IsAssignableTo(parameters[0].ParameterType);
                                       }
                                   );

        return methodInfo;
    }

    public static ValueTask SendEnteringAsync(IShellProxy shell, Page page, object? intent, INavigationConfiguration configuration)
    {
        var context = PageNavigationContext.Get(page);

        if (context.Entered)
        {
            return ValueTask.CompletedTask;
        }

        context.Entered = true;
        var target = GetLifecycleTarget(page);

        if (intent is not null)
        {
            var intentType = intent.GetType();
            
#pragma warning disable IL2072 // All page models have been registered with NavigationConfigurator.DynamicallyAccessedPageModelMembers
            var enteringWithIntentMethod = GetImplementedLifecycleMethod(OnEnteringAsyncRegex(), target.GetType(), intentType);
#pragma warning restore IL2072

            if (enteringWithIntentMethod is not null)
            {
#if DEBUG
                Console.WriteLine($"Entering {target.GetType().FullName} with intent {intent.GetType().FullName}");
#endif
                shell.SendNavigationLifecycleEvent(
                    new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Entering, target, NavigationLifecycleHandling.HandledWithIntent, intent)
                );

                return WithLifecyclePageAsync(page, () => (ValueTask) enteringWithIntentMethod.Invoke(target, [intent])!);
            }

            if (configuration.NavigationIntentBehavior == NavigationIntentBehavior.Strict)
            {
                shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Entering, target, NavigationLifecycleHandling.NotHandled, intent));

                return ValueTask.CompletedTask;
            }
        }

        if (target is IEnteringAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Entering {target.GetType().FullName}");
#endif
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Entering, target, NavigationLifecycleHandling.Handled, intent));

            return WithLifecyclePageAsync(page, enteringAware.OnEnteringAsync);
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Entering, target, NavigationLifecycleHandling.NotHandled, intent));

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendLeavingAsync(IShellProxy shell, Page page)
    {
        var context = PageNavigationContext.Get(page);

        if (!context.Entered)
        {
            return ValueTask.CompletedTask;
        }

        context.Entered = false;

        var target = GetLifecycleTarget(page);

        if (target is ILeavingAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Leaving {target.GetType().FullName}");
#endif
            // ReSharper disable once RedundantArgumentDefaultValue
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Leaving, target, NavigationLifecycleHandling.Handled));

            return WithLifecyclePageAsync(page, enteringAware.OnLeavingAsync);
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Leaving, target, NavigationLifecycleHandling.NotHandled));

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendAppearingAsync(IShellProxy shell, Page page, object? intent, INavigationConfiguration configuration)
    {
        var context = PageNavigationContext.Get(page);

        if (context.Appeared)
        {
            return ValueTask.CompletedTask;
        }

        context.Appeared = true;

        var target = GetLifecycleTarget(page);

        if (intent is not null)
        {
            var intentType = intent.GetType();
#pragma warning disable IL2072 // All page models have been registered with NavigationConfigurator.DynamicallyAccessedPageModelMembers
            var appearingWithIntentMethod = GetImplementedLifecycleMethod(OnAppearingAsyncRegex(), target.GetType(), intentType);
#pragma warning restore IL2072

            if (appearingWithIntentMethod is not null)
            {
#if DEBUG
                Console.WriteLine($"Appearing {target.GetType().FullName} with intent {intent.GetType().FullName}");
#endif
                shell.SendNavigationLifecycleEvent(
                    new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, target, NavigationLifecycleHandling.HandledWithIntent, intent)
                );

                return WithLifecyclePageAsync(page, () => (ValueTask) appearingWithIntentMethod.Invoke(target, [intent])!);
            }

            if (configuration.NavigationIntentBehavior == NavigationIntentBehavior.Strict)
            {
                shell.SendNavigationLifecycleEvent(
                    new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, target, NavigationLifecycleHandling.NotHandled, intent)
                );

                return ValueTask.CompletedTask;
            }
        }

        if (target is IAppearingAware appearingAware)
        {
#if DEBUG
            Console.WriteLine($"Appearing {target.GetType().FullName}");
#endif
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, target, NavigationLifecycleHandling.Handled, intent));

            return WithLifecyclePageAsync(page, appearingAware.OnAppearingAsync);
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Appearing, target, NavigationLifecycleHandling.NotHandled, intent));

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendDisappearingAsync(IShellProxy shell, Page page)
    {
        var context = PageNavigationContext.Get(page);

        if (!context.Appeared)
        {
            return ValueTask.CompletedTask;
        }

        context.Appeared = false;

        var target = GetLifecycleTarget(page);

        if (target is IDisappearingAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Disappearing {target.GetType().FullName}");
#endif
            // ReSharper disable once RedundantArgumentDefaultValue
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Disappearing, target, NavigationLifecycleHandling.Handled));

            return WithLifecyclePageAsync(page, enteringAware.OnDisappearingAsync);
        }

        shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.Disappearing, target, NavigationLifecycleHandling.NotHandled));

        return ValueTask.CompletedTask;
    }

    public static ValueTask<bool> CanLeaveAsync(IShellProxy shell, Page page)
    {
        var target = GetLifecycleTarget(page);

        if (target is ILeavingGuard leavingGuard)
        {
#if DEBUG
            Console.WriteLine($"Can leave {target.GetType().FullName}");
#endif
            shell.SendNavigationLifecycleEvent(new NavigationLifecycleEventArgs(NavigationLifecycleEventType.LeavingGuard, target));

            return leavingGuard.CanLeaveAsync();
        }

        return ValueTask.FromResult(true);
    }

    public static void AssertIntent(Page page, object? intent, Action? onFailure = null)
    {
        if (intent is null)
        {
            return;
        }

        var pageModelType = GetLifecycleTarget(page).GetType();
        var intentType = intent.GetType();

#pragma warning disable IL2072 // All page models have been registered with NavigationConfigurator.DynamicallyAccessedPageModelMembers
        if (GetImplementedLifecycleMethod(OnEnteringAsyncRegex(), pageModelType, intentType) is not null)
        {
            return;
        }

        if (GetImplementedLifecycleMethod(OnAppearingAsyncRegex(), pageModelType, intentType) is not null)
        {
            return;
        }
#pragma warning restore IL2072

        onFailure?.Invoke();

        var intentTypeFullName = intentType.FullName;
        throw new InvalidOperationException(
            $"{pageModelType.FullName} must implement either IEnteringAware<{intentTypeFullName}> or IAppearingAware<{intentTypeFullName}> to receive intent."
        );
    }

    public static string GetSegmentName(INavigationSegment segment, INavigationConfiguration configuration)
        => segment.SegmentName ?? NavigationSegmentAttribute.GetSegmentName(GetPageType(segment.Type, configuration));

    public static Type GetPageType(Type? segmentType, INavigationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(segmentType);

        if (segmentType.IsSubclassOf(typeof(Page)))
        {
            return segmentType;
        }

        if (configuration.Mapping.TryGetValue(segmentType, out var pageType))
        {
            return pageType;
        }

        throw new InvalidOperationException($"Cannot find page type for segment type {segmentType.FullName}.");
    }

    [GeneratedRegex("^(Nalu\\.IEnteringAware<.+>\\.|^)OnEnteringAsync$")]
    private static partial Regex OnEnteringAsyncRegex();
    
    [GeneratedRegex("^(Nalu\\.IAppearingAware<.+>\\.|^)OnAppearingAsync$")]
    private static partial Regex OnAppearingAsyncRegex();
}
