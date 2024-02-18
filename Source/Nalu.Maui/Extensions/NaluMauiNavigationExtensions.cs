// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

using Nalu;

/// <summary>
/// Navigation extensions for default MAUI navigation pattern.
/// </summary>
public static class NaluMauiNavigationExtensions
{
    /// <summary>
    /// Ensure page are disposed when they are popped from the navigation stack.
    /// </summary>
    /// <remarks>
    /// Shell items will not be disposed.
    /// Do NOT use this method if you are using Nalu navigation.
    /// </remarks>
    /// <param name="shell">The shell instance.</param>
    public static Shell ConfigureForPageDisposal(this Shell shell)
    {
        if (shell is NaluShell)
        {
            throw new InvalidOperationException("Nalu navigation already handles page disposal.");
        }

        var navigationStack = GetNavigationStack(shell.Navigation);
        shell.Navigated += ShellOnNavigated;

        return shell;

        void ShellOnNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            var newNavigationStack = GetNavigationStack(shell.Navigation);
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
