// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

/// <summary>
/// Navigation extensions for default MAUI navigation pattern.
/// </summary>
public static class NaluMauiNavigationExtensions
{
    /// <summary>
    /// Ensure page and view models are disposed when they are removed from shell sections navigation stacks.
    /// </summary>
    /// <typeparam name="T">Type of shell to configure.</typeparam>
    /// <param name="shell">The shell instance.</param>
    public static T ConfigureForPageDisposal<T>(this T shell)
        where T : Shell
    {
        var navigationStacks = shell.Items
            .SelectMany(item => item.Items)
            .SelectMany(section => GetNavigationStack(section.Navigation))
            .ToHashSet();

        shell.Navigated += ShellOnNavigated;

        return shell;

#pragma warning disable VSTHRD100
        void ShellOnNavigated(object? sender, ShellNavigatedEventArgs e)
#pragma warning restore VSTHRD100
        {
            var newNavigationStacks = shell.Items
                .SelectMany(item => item.Items)
                .SelectMany(section => GetNavigationStack(section.Navigation))
                .ToHashSet();
            var pagesToDispose = navigationStacks.Except(newNavigationStacks);
            navigationStacks = newNavigationStacks;

            foreach (var page in pagesToDispose)
            {
                if (page.BindingContext is IDisposable disposableBindingContext)
                {
                    disposableBindingContext.Dispose();
                }

                if (page is IDisposable disposablePage)
                {
                    disposablePage.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Ensure page are disposed when they are popped from the navigation stack.
    /// </summary>
    /// <param name="navigationPage">The navigation page instance.</param>
    public static NavigationPage ConfigureForPageDisposal(this NavigationPage navigationPage)
    {
        var navigationStack = GetNavigationStack(navigationPage.Navigation);
        navigationPage.NavigatedTo += NavigationPageOnNavigatedTo;

        return navigationPage;

        void NavigationPageOnNavigatedTo(object? sender, NavigatedToEventArgs e)
        {
            var newNavigationStack = GetNavigationStack(navigationPage.Navigation);
            var pagesToDispose = navigationStack.Except(newNavigationStack);
            navigationStack = newNavigationStack;

            foreach (var page in pagesToDispose)
            {
                if (page.BindingContext is IDisposable disposableBindingContext)
                {
                    disposableBindingContext.Dispose();
                }

                if (page is IDisposable disposablePage)
                {
                    disposablePage.Dispose();
                }
            }
        }
    }

    private static HashSet<Page> GetNavigationStack(INavigation navigation)
        => navigation.NavigationStack
            .Concat(navigation.ModalStack)
            .Where(p => p is not null).ToHashSet();
}
