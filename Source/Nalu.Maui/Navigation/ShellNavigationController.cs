namespace Nalu;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

[ExcludeFromCodeCoverage]
internal sealed class ShellNavigationController(INavigationServiceInternal navigationService, INavigationOptions navigationOptions, NaluShell shell) : IShellNavigationController, IDisposable
{
    private static readonly PropertyInfo _shellContentCacheProperty = typeof(ShellContent).GetProperty("ContentCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<Page> _navigationStack = new(10);
    private readonly List<Page> _leakDetectionPages = new(10);

    public Page CurrentPage => _navigationStack[^1];

    public Page RootPage => _navigationStack[0];

    public IReadOnlyList<Page> NavigationStack => _navigationStack;

    public void Dispose() => _semaphore.Dispose();

    public void ConfigurePage(Page page)
    {
        var backButtonBehavior = new BackButtonBehavior
        {
            Command = new Command(() => _ = navigationService.GoToAsync(Navigation.Relative().Pop())),
            IconOverride = navigationOptions.BackImage,
        };

        if (backButtonBehavior.IconOverride is FontImageSource fontImageSource)
        {
            fontImageSource.Color = Shell.GetForegroundColor(shell);
        }

        Shell.SetBackButtonBehavior(page, backButtonBehavior);
    }

    async Task<T> IShellNavigationController.ExecuteNavigationAsync<T>(Func<Task<T>> navigationFunc)
    {
        await _semaphore.WaitAsync().ConfigureAwait(true);
        try
        {
            var result = await navigationFunc().ConfigureAwait(true);
            if (Debugger.IsAttached)
            {
                _ = new LeakDetector(_leakDetectionPages).EnsureCollectedAsync();
                _leakDetectionPages.Clear();
            }

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task PopAsync(int times)
    {
#if IOS
        await WaitForModalInPresentationAsync().ConfigureAwait(true);
#endif
        try
        {
            shell.SetIsNavigating(true);
            shell.FlyoutIsPresented = false;

            // We can't use shell.GoToAsync("../../../") because Tabs are buggy when popping multiple times in a row
            while (--times >= 0)
            {
                await shell.GoToAsync("../", true).ConfigureAwait(true);
                var removedIndex = _navigationStack.Count - 1;
                var removedPage = _navigationStack[removedIndex];
                _navigationStack.RemoveAt(removedIndex);
                PageNavigationContext.Dispose(removedPage);
                _leakDetectionPages.Add(removedPage);
            }
        }
        finally
        {
            shell.SetIsNavigating(false);
        }
    }

    public async Task PushAsync(Page page)
    {
#if IOS
        await WaitForModalInPresentationAsync().ConfigureAwait(true);
#endif
        try
        {
            shell.SetIsNavigating(true);
            shell.FlyoutIsPresented = false;
            var route = $"{shell.CurrentState.Location}/{NavigationHelper.GetSegmentName(page.BindingContext.GetType())}";
            Routing.UnRegisterRoute(route);
            Routing.RegisterRoute(route, new FixedRouteFactory(page));
            await shell.GoToAsync(route).ConfigureAwait(true);
            _navigationStack.Add(page);
        }
        finally
        {
            shell.SetIsNavigating(false);
        }
    }

    public Page? GetRootPage(string segmentName)
    {
        var shellContent = (IShellContentController?)shell.Items
            .SelectMany(item => item.Items)
            .SelectMany(section => section.Items)
            .FirstOrDefault(content => content.Route == segmentName);
        return shellContent?.GetOrCreateContent();
    }

    public async Task SetRootPageAsync(string segmentName)
    {
#if IOS
        await WaitForModalInPresentationAsync().ConfigureAwait(true);
#endif
        try
        {
            shell.SetIsNavigating(true);

            await SetShellContentPageAsync(segmentName).ConfigureAwait(true);

            _navigationStack.Clear();
            _navigationStack.Add(shell.CurrentPage);
        }
        finally
        {
            shell.SetIsNavigating(false);
        }
    }

    private async Task SetShellContentPageAsync(string segmentName)
    {
        const int shellAnimationDuration = 300;
        var beforeNavigationSection = shell.CurrentItem.CurrentItem;
        var beforeNavigationContent = beforeNavigationSection.CurrentItem;

        await shell.GoToAsync($"//{segmentName}", true).ConfigureAwait(true);

        var afterNavigationSection = shell.CurrentItem.CurrentItem;
        var afterNavigationContent = afterNavigationSection.CurrentItem;

        // We want to remove and dispose the page we navigated from
        // This should happen only when *not* navigating between tabs (in the same section)
        if (beforeNavigationContent != afterNavigationContent)
        {
            if (beforeNavigationSection is Tab)
            {
                if (afterNavigationSection != beforeNavigationSection)
                {
                    // Shell navigation does not wait for the animation to finish
                    await Task.Delay(shellAnimationDuration).ConfigureAwait(true);

                    foreach (var shellContent in beforeNavigationSection.Items)
                    {
                        RemoveShellContentPage(shellContent);
                    }
                }
            }
            else
            {
                // Shell navigation does not wait for the animation to finish
                await Task.Delay(shellAnimationDuration).ConfigureAwait(true);
                RemoveShellContentPage(beforeNavigationContent);
            }
        }
    }

    private void RemoveShellContentPage(ShellContent shellContent)
    {
        var page = (Page?)_shellContentCacheProperty.GetValue(shellContent);
        if (page is not null)
        {
            PageNavigationContext.Dispose(page);
            _shellContentCacheProperty.SetValue(shellContent, null);
            _leakDetectionPages.Add(page);
        }
    }

#if IOS
    private static async Task WaitForModalInPresentationAsync()
    {
        // If application is in the middle of presenting a modal, wait for it to finish
        var application = Application.Current;
        var appDelegate = (MauiUIApplicationDelegate)application!.Handler!.PlatformView!;
        var rootViewController = appDelegate.Window?.RootViewController;

        if (rootViewController is null)
        {
            return;
        }

#pragma warning disable CA1422 // Somehow the compiler is not smart enough to understand the else block is referring to iOS < 13
        var isIOS13 = OperatingSystem.IsOSPlatformVersionAtLeast("iOS", 13);
        while (isIOS13 ? rootViewController.ModalInPresentation : rootViewController.ModalInPopover)
#pragma warning restore CA1422
        {
            await Task.Delay(50).ConfigureAwait(true);
        }
    }
#endif
}
