namespace Nalu;

/// <summary>
/// Provides a navigation layer used by <see cref="NavigationService"/> to perform navigation.
/// </summary>
internal interface INavigationController
{
    /// <summary>
    /// Gets the navigation stack.
    /// </summary>
    IReadOnlyList<Page> NavigationStack { get; }

    /// <summary>
    /// Executes the navigation preventing concurrent navigations.
    /// </summary>
    /// <param name="navigationFunc">The navigation action to perform.</param>
    /// <typeparam name="T">Async function return type.</typeparam>
    Task<T> ExecuteNavigationAsync<T>(Func<Task<T>> navigationFunc);

    /// <summary>
    /// Gets the current page displayed in the navigation.
    /// </summary>
    Page CurrentPage { get; }

    /// <summary>
    /// Gets the root page.
    /// </summary>
    Page RootPage { get; }

    /// <summary>
    /// Sets the root page of the navigation.
    /// </summary>
    /// <param name="page">The page to set as root.</param>
    Task SetRootPageAsync(Page page);

    /// <summary>
    /// Configures page for the navigation.
    /// </summary>
    /// <param name="page">The page to be configured.</param>
    void ConfigurePage(Page page);

    /// <summary>
    /// Pops the current page from the navigation stack.
    /// </summary>
    /// <param name="times">How many times to pop.</param>
    Task PopAsync(int times = 1);

    /// <summary>
    /// Pushes a page onto the navigation stack.
    /// </summary>
    /// <param name="page">The page to be pushed.</param>
    Task PushAsync(Page page);
}
