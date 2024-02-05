namespace Nalu;

using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
internal sealed class ShellNavigationController(INavigationService navigationService, INavigationOptions navigationOptions, NaluShell shell) : INavigationController, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<Page> _navigationStack = new(10);

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

    async Task<T> INavigationController.ExecuteNavigationAsync<T>(Func<Task<T>> navigationFunc)
    {
        try
        {
            // We want the eventual button animation to appear before navigating
            // on top of that we want to avoid other kind of race conditions
            await Task.Yield();

            await _semaphore.WaitAsync().ConfigureAwait(true);
#if IOS
            await WaitForModalInPresentationAsync().ConfigureAwait(true);
#endif
            return await navigationFunc().ConfigureAwait(true);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task PopAsync(int times)
    {
        try
        {
            shell.SetIsNavigating(true);
            shell.FlyoutIsPresented = false;

            // We can't use shell.GoToAsync because Tabs are buggy when popping multiple times
            while (--times >= 0)
            {
                await shell.GoToAsync("../", true).ConfigureAwait(true);
                _navigationStack.RemoveAt(_navigationStack.Count - 1);
            }
        }
        finally
        {
            shell.SetIsNavigating(false);
        }
    }

    public async Task PushAsync(Page page)
    {
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

    private class FixedRouteFactory : RouteFactory
    {
        private readonly WeakReference<Page> _weakPage;

#pragma warning disable IDE0290
        public FixedRouteFactory(Page page)
#pragma warning restore IDE0290
        {
            _weakPage = new WeakReference<Page>(page);
        }

        public override Element GetOrCreate() => _weakPage.TryGetTarget(out var page) ? page : throw new InvalidOperationException("Page has been collected");

        public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
    }

    public async Task SetRootPageAsync(Page page)
    {
        try
        {
            shell.SetIsNavigating(true);

            SetRootBackButtonBehavior(page);

            var segmentName = NavigationHelper.GetSegmentName(page.BindingContext.GetType());

            await SetShellContentPageAsync(segmentName, page).ConfigureAwait(true);

            _navigationStack.Clear();
            _navigationStack.Add(page);
        }
        finally
        {
            shell.SetIsNavigating(false);
        }
    }

    private async Task SetShellContentPageAsync(string segmentName, Page page)
    {
        if (!await InternalSetShellContentPageAsync(segmentName, page).ConfigureAwait(true))
        {
            throw new KeyNotFoundException($"Could not find shell content for page route {segmentName}");
        }
    }

    private async Task<bool> InternalSetShellContentPageAsync(string segmentName, Page page)
    {
        foreach (var item in shell.Items)
        {
            foreach (var section in item.Items)
            {
                var i = 0;
                foreach (var content in section.Items)
                {
                    if (content.Route == segmentName)
                    {
                        content.Content = page;

                        // Updating shell content is not working on Tabs
                        // https://github.com/dotnet/maui/issues/12669
                        // Workaround: remove and insert the shell content on the same position
                        if (section is Tab)
                        {
                            await Task.Yield();
                            section.Items.RemoveAt(i);
                            section.Items.Insert(i, content);
                            await Task.Yield();
                        }

                        shell.FlyoutIsPresented = false;
                        await shell.GoToAsync($"//{segmentName}", true).ConfigureAwait(true);

                        return true;
                    }

                    ++i;
                }
            }
        }

        return false;
    }

    private void SetRootBackButtonBehavior(Page page)
    {
        var backButtonBehavior = Shell.GetBackButtonBehavior(page);

#if ANDROID
        // https://github.com/dotnet/maui/issues/7045
        backButtonBehavior.Command = null;
#else
        backButtonBehavior.Command = new Command(() => _ = shell.FlyoutIsPresented = true);
#endif
        backButtonBehavior.IconOverride = navigationOptions.MenuImage;

        if (backButtonBehavior.IconOverride is FontImageSource fontImageSource)
        {
            fontImageSource.Color = Shell.GetForegroundColor(shell);
        }
    }

#if IOS
    private static async Task WaitForModalInPresentationAsync()
    {
        // If application is in the middle of presenting a modal, wait for it to finish
        var application = Application.Current;
        var appDelegate = (MauiUIApplicationDelegate)application!.Handler!.PlatformView!;
        var rootViewController = appDelegate.Window!.RootViewController!;

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
