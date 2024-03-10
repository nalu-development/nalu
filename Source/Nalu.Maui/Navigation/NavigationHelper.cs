namespace Nalu;

using System.Reflection;

internal static class NavigationHelper
{
    private static readonly MethodInfo _sendEnteringWithIntentAsyncMethod = typeof(NavigationHelper).GetMethod(nameof(SendEnteringWithIntentAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _sendAppearingWithIntentAsyncMethod = typeof(NavigationHelper).GetMethod(nameof(SendAppearingWithIntentAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ValueTask SendEnteringAsync(Page page, object? intent)
    {
        var context = PageNavigationContext.Get(page);
        if (context.Entered)
        {
            return ValueTask.CompletedTask;
        }

        context.Entered = true;
        var target = page.BindingContext;

        if (intent is not null)
        {
            var intentType = intent.GetType();
            var enteringWithIntentType = typeof(IEnteringAware<>).MakeGenericType(intentType);
            if (enteringWithIntentType.IsInstanceOfType(target))
            {
#if DEBUG
                Console.WriteLine($"Entering {target.GetType().FullName} with intent {intent.GetType().FullName}");
#endif
                return (ValueTask)_sendEnteringWithIntentAsyncMethod.MakeGenericMethod(intentType).Invoke(null, [target, intent])!;
            }

            return ValueTask.CompletedTask;
        }

        if (target is IEnteringAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Entering {target.GetType().FullName}");
#endif
            return enteringAware.OnEnteringAsync();
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendLeavingAsync(Page page)
    {
        var context = PageNavigationContext.Get(page);
        if (!context.Entered)
        {
            return ValueTask.CompletedTask;
        }

        context.Entered = false;

        var target = page.BindingContext;

        if (target is ILeavingAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Leaving {target.GetType().FullName}");
#endif
            return enteringAware.OnLeavingAsync();
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendAppearingAsync(Page page, object? intent)
    {
        var context = PageNavigationContext.Get(page);
        if (context.Appeared)
        {
            return ValueTask.CompletedTask;
        }

        context.Appeared = true;

        var target = page.BindingContext;

        if (intent is not null)
        {
            var intentType = intent.GetType();
            var appearingWithIntentType = typeof(IAppearingAware<>).MakeGenericType(intentType);
            if (appearingWithIntentType.IsInstanceOfType(target))
            {
#if DEBUG
                Console.WriteLine($"Appearing {target.GetType().FullName} with intent {intent.GetType().FullName}");
#endif
                return (ValueTask)_sendAppearingWithIntentAsyncMethod.MakeGenericMethod(intentType).Invoke(null, [target, intent])!;
            }

            return ValueTask.CompletedTask;
        }

        if (target is IAppearingAware appearingAware)
        {
#if DEBUG
            Console.WriteLine($"Appearing {target.GetType().FullName}");
#endif
            return appearingAware.OnAppearingAsync();
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendDisappearingAsync(Page page)
    {
        var context = PageNavigationContext.Get(page);
        if (!context.Appeared)
        {
            return ValueTask.CompletedTask;
        }

        context.Appeared = false;

        var target = page.BindingContext;

        if (target is IDisappearingAware enteringAware)
        {
#if DEBUG
            Console.WriteLine($"Disappearing {target.GetType().FullName}");
#endif
            return enteringAware.OnDisappearingAsync();
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask<bool> CanLeaveAsync(Page page)
    {
        var target = page.BindingContext;

        if (target is ILeavingGuard leavingGuard)
        {
#if DEBUG
            Console.WriteLine($"Can leave {target.GetType().FullName}");
#endif
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

        var pageModelType = page.BindingContext?.GetType();
        if (pageModelType is null)
        {
            return;
        }

        var intentType = intent.GetType();
        var enteringWithIntentType = typeof(IEnteringAware<>).MakeGenericType(intentType);
        if (enteringWithIntentType.IsAssignableFrom(pageModelType))
        {
            return;
        }

        var appearingWithIntentType = typeof(IAppearingAware<>).MakeGenericType(intentType);
        if (appearingWithIntentType.IsAssignableFrom(pageModelType))
        {
            return;
        }

        onFailure?.Invoke();

        throw new InvalidOperationException($"{pageModelType.FullName} must implement either {enteringWithIntentType.FullName} or {appearingWithIntentType.FullName} to receive intent.");
    }

    public static string GetSegmentName(INavigationSegment segment, INavigationConfiguration configuration)
        => segment.SegmentName ?? NavigationSegmentAttribute.GetSegmentName(GetPageType(segment.Type, configuration));

    public static Type GetPageType(Type? segmentType, INavigationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(segmentType, nameof(segmentType));

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

    private static ValueTask SendEnteringWithIntentAsync<TIntent>(IEnteringAware<TIntent> target, TIntent intent) => target.OnEnteringAsync(intent);
    private static ValueTask SendAppearingWithIntentAsync<TIntent>(IAppearingAware<TIntent> target, TIntent intent) => target.OnAppearingAsync(intent);
}
