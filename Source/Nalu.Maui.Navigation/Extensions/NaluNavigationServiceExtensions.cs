namespace Nalu;

/// <summary>
/// Syntax sugar extensions for the <see cref="INavigationService"/> 
/// </summary>
public static class NaluNavigationServiceExtensions
{
    /// <summary>
    /// Pushes <typeparamref name="TPage"/> via <see cref="RelativeNavigation"/> to resolve the given <paramref name="intent"/>.
    /// </summary>
    /// <remarks>
    /// It is expected that <typeparamref name="TPage"/> resolves the <paramref name="intent"/> via <see cref="AwaitableIntent{TResult}.SetResult"/> or marks it as faulty via <see cref="AwaitableIntent{TResult}.SetException"/>.
    /// When the intent is not resolved, the task will complete with default(<typeparamref name="TResult" />).
    /// </remarks>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="intent">The intent to be resolved.</param>
    /// <typeparam name="TPage">The page/page model responsible for resolving the intent.</typeparam>
    /// <typeparam name="TResult">The return type.</typeparam>
    /// <returns>A task which completes when <typeparamref name="TPage" /> is removed from the navigation stack containing the result provided via <see cref="AwaitableIntent{TResult}.SetResult"/> or default(<typeparamref name="TResult" />) when not provided.</returns>
    /// <exception cref="Exception">May throw an exception if <typeparamref name="TPage" /> chose to fault the intent.</exception>
    public static async Task<TResult> ResolveIntentAsync<TPage, TResult>(this INavigationService navigationService, AwaitableIntent<TResult> intent)
        where TPage : class
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(navigationService);

        await navigationService.GoToAsync(Navigation.Relative().Push<TPage>().WithIntent(intent));

        return await intent;
    }

    /// <summary>
    /// Pushes <typeparamref name="TPage"/> via <see cref="RelativeNavigation"/> to resolve the given <paramref name="intent"/>.
    /// </summary>
    /// <param name="navigationService">The navigation service.</param>
    /// <param name="intent">The intent to be resolved.</param>
    /// <typeparam name="TPage">The page/page model responsible for resolving the intent.</typeparam>
    /// <returns>A task which completes when <typeparamref name="TPage" /> is removed from the navigation stack.</returns>
    /// <exception cref="Exception">May throw an exception if <typeparamref name="TPage" /> chose to fault the intent.</exception>
    public static async Task ResolveIntentAsync<TPage>(this INavigationService navigationService, AwaitableIntent intent)
        where TPage : class
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(navigationService);

        await navigationService.GoToAsync(Navigation.Relative().Push<TPage>().WithIntent(intent));

        await intent;
    }
}
