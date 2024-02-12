namespace Nalu;

using System.ComponentModel;
using System.Reflection;

internal static class NavigationHelper
{
    private static readonly MethodInfo _sendEnteringWithIntentAsyncMethod = typeof(NavigationHelper).GetMethod(nameof(SendEnteringWithIntentAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _sendAppearingWithIntentAsyncMethod = typeof(NavigationHelper).GetMethod(nameof(SendAppearingWithIntentAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly Dictionary<Type, string> _typeSegmentNames = [];

    public static string GetSegmentName(Type type)
    {
        if (!_typeSegmentNames.TryGetValue(type, out var segmentName))
        {
            segmentName = type.GetCustomAttribute<NavigationSegmentAttribute>()?.SegmentName ?? type.Name;
            _typeSegmentNames.Add(type, segmentName);
        }

        return segmentName;
    }

    public static ValueTask SendEnteringAsync(Page page, object? intent)
    {
        var target = page.BindingContext;

        if (intent is null)
        {
            if (target is IEnteringAware enteringAware)
            {
#if DEBUG
                Console.WriteLine($"Entering {target.GetType().FullName}");
#endif
                return enteringAware.OnEnteringAsync();
            }

            return ValueTask.CompletedTask;
        }

        var intentType = intent.GetType();
        var enteringWithIntentType = typeof(IEnteringAware<>).MakeGenericType(intentType);
        if (enteringWithIntentType.IsInstanceOfType(target))
        {
#if DEBUG
            Console.WriteLine($"Entering {target.GetType().FullName} with intent {intent.GetType().FullName}");
#endif
            return (ValueTask)_sendEnteringWithIntentAsyncMethod.MakeGenericMethod(intentType).Invoke(null, new[] { target, intent })!;
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendLeavingAsync(Page page)
    {
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
        var target = page.BindingContext;

        if (intent is null)
        {
            if (target is IAppearingAware appearingAware)
            {
#if DEBUG
                Console.WriteLine($"Appearing {target.GetType().FullName}");
#endif
                return appearingAware.OnAppearingAsync();
            }

            return ValueTask.CompletedTask;
        }

        var intentType = intent.GetType();
        var appearingWithIntentType = typeof(IAppearingAware<>).MakeGenericType(intentType);
        if (appearingWithIntentType.IsInstanceOfType(target))
        {
#if DEBUG
            Console.WriteLine($"Appearing {target.GetType().FullName} with intent {intent.GetType().FullName}");
#endif
            return (ValueTask)_sendAppearingWithIntentAsyncMethod.MakeGenericMethod(intentType).Invoke(null, new[] { target, intent })!;
        }

        return ValueTask.CompletedTask;
    }

    public static ValueTask SendDisappearingAsync(Page page)
    {
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

    public static void AssertIntentAware(Type? pageModelType, object? intent)
    {
        if (intent is null)
        {
            return;
        }

        if (pageModelType is null)
        {
            throw new InvalidOperationException($"Cannot navigate with intent {intent.GetType().FullName} when target page model is not set.");
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

        throw new InvalidOperationException($"{pageModelType.FullName} must implement either {enteringWithIntentType.FullName} or {appearingWithIntentType.FullName} to receive intent.");
    }

    public static IEnumerable<INotifyPropertyChanged> EnumerateStackPageModels(IShellNavigationController controller)
        => controller.NavigationStack
            .Select(page => page.BindingContext)
            .OfType<INotifyPropertyChanged>();

    private static ValueTask SendEnteringWithIntentAsync<TIntent>(IEnteringAware<TIntent> target, TIntent intent) => target.OnEnteringAsync(intent);
    private static ValueTask SendAppearingWithIntentAsync<TIntent>(IAppearingAware<TIntent> target, TIntent intent) => target.OnAppearingAsync(intent);
}
