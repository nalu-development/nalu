// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

using System.Reflection;
using Nalu;

/// <summary>
/// Navigation extensions for default MAUI navigation pattern.
/// </summary>
public static class NaluMauiNavigationExtensions
{
    private static readonly PropertyInfo _shellContentCacheProperty = typeof(ShellContent).GetProperty("ContentCache", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>
    /// Ensure page are disposed when they are popped from the navigation stack.
    /// </summary>
    /// <remarks>
    /// When disposing shell contents, Tab contents will be disposed only when navigating away from tabs.
    /// Also, only ShellContent with ContentTemplate will be disposed.
    /// </remarks>
    /// <param name="shell">The shell instance.</param>
    /// <param name="disposeShellContents">Whether to dispose ShellContent after navigating to a different one.</param>
    public static Shell ConfigureForPageDisposal(this Shell shell, bool disposeShellContents = false)
    {
        if (shell is NaluShell)
        {
            throw new InvalidOperationException("Nalu navigation already handles page disposal.");
        }

        var shellContents = new HashSet<ShellContent> { shell.CurrentItem.CurrentItem.CurrentItem };
        var navigationStack = GetNavigationStack(shell.Navigation);
        shell.Navigated += ShellOnNavigated;

        return shell;

#pragma warning disable VSTHRD100
        async void ShellOnNavigated(object? sender, ShellNavigatedEventArgs e)
#pragma warning restore VSTHRD100
        {
            var newNavigationStack = GetNavigationStack(shell.Navigation);
            var pagesToDispose = navigationStack.Except(newNavigationStack).ToHashSet();
            navigationStack = newNavigationStack;

            // We want to remove and dispose the shell content page we navigated from
            // This should happen only when *not* navigating between tabs (in the same section)
            if (disposeShellContents)
            {
                var currentShellContent = shell.CurrentItem.CurrentItem.CurrentItem;
                if (!(currentShellContent.Parent is Tab && currentShellContent.Parent == shellContents.First().Parent))
                {
                    var previousShellContents = shellContents.ToList();
                    shellContents.Clear();
                    shellContents.Add(currentShellContent);

                    // Let if finish the navigation process
                    await Task.Yield();
                    await Task.Yield();

                    foreach (var content in previousShellContents)
                    {
                        // Only shell content with templates can be disposed and recreated
                        if (content.ContentTemplate != null)
                        {
                            var page = ((IShellContentController)content).Page;
                            pagesToDispose.Add(page);
                            _shellContentCacheProperty.SetValue(content, null);
                        }
                    }
                }
                else
                {
                    shellContents.Add(currentShellContent);
                }
            }

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
