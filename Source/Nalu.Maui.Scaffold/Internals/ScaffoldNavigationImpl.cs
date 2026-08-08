namespace Nalu;

/// <summary>
/// The Scaffold's <see cref="INavigation"/> implementation, installed as the scaffold's
/// NavigationProxy inner — the same integration point Shell and NavigationPage use. Hosted pages
/// chain to it automatically (they are logical children), so <c>page.Navigation</c> — used by
/// developer code and automation drivers alike — reports the truthful stack and routes pops
/// through the Nalu navigation engine (guards and lifecycle always run).
/// </summary>
/// <remarks>
/// Only reads and pops are supported: pushing arbitrary page instances does not fit Nalu's
/// type-based, DI-scoped navigation model — use <c>INavigationService</c> for forward navigation.
/// </remarks>
internal sealed class ScaffoldNavigationImpl(Scaffold scaffold) : INavigation
{
    private const string _unsupportedMessage =
        "This operation is not supported by the Nalu Scaffold: use INavigationService (Navigation.Relative()/Navigation.Absolute()) instead.";

    private ScaffoldNavigationStack? CurrentStack
        => (scaffold.Proxy?.CurrentItem.CurrentSection as ScaffoldRootProxy)?.Root.NavigationStack;

    public IReadOnlyList<Page> NavigationStack
    {
        get
        {
            if (CurrentStack is not { RootPage: { } rootPage } stack)
            {
                return [];
            }

            var pages = new List<Page>(stack.PushedPages.Count + 1) { rootPage };
            pages.AddRange(stack.PushedPages.Select(entry => entry.Page));

            return pages;
        }
    }

    public IReadOnlyList<Page> ModalStack => [];

    public Task<Page> PopAsync() => PopAsync(true);

    public async Task<Page> PopAsync(bool animated)
    {
        if (scaffold.NavigationService is not { } navigationService
            || CurrentStack is not { PushedPages.Count: > 0 } stack)
        {
            return null!;
        }

        var topPage = stack.PushedPages[^1].Page;
        var popped = await navigationService.GoToAsync(Navigation.Relative().Pop());

        return popped ? topPage : null!;
    }

    public Task PopToRootAsync() => PopToRootAsync(true);

    public async Task PopToRootAsync(bool animated)
    {
        if (scaffold.NavigationService is not { } navigationService
            || CurrentStack is not { PushedPages.Count: > 0 and var popCount })
        {
            return;
        }

        var navigation = Navigation.Relative();

        for (var i = 0; i < popCount; i++)
        {
            navigation.Pop();
        }

        await navigationService.GoToAsync(navigation);
    }

    public Task PushAsync(Page page) => throw new NotSupportedException(_unsupportedMessage);

    public Task PushAsync(Page page, bool animated) => throw new NotSupportedException(_unsupportedMessage);

    public Task PushModalAsync(Page page) => throw new NotSupportedException(_unsupportedMessage);

    public Task PushModalAsync(Page page, bool animated) => throw new NotSupportedException(_unsupportedMessage);

    public Task<Page> PopModalAsync() => throw new NotSupportedException(_unsupportedMessage);

    public Task<Page> PopModalAsync(bool animated) => throw new NotSupportedException(_unsupportedMessage);

    public void InsertPageBefore(Page page, Page before) => throw new NotSupportedException(_unsupportedMessage);

    public void RemovePage(Page page) => throw new NotSupportedException(_unsupportedMessage);
}
