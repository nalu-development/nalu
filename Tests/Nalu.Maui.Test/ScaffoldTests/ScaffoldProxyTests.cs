using System.ComponentModel;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// P0 seam tests: the REAL <see cref="ScaffoldProxy"/> driven by the REAL
/// <see cref="NavigationService"/> — proving the Scaffold host satisfies the engine contracts
/// (structure, batching, single presenter synchronization per commit, selection state).
/// </summary>
public class ScaffoldProxyTests
{
    public interface IHomePageModel : INotifyPropertyChanged;

    public sealed record ScaffoldStartupIntent;

    public interface ISearchPageModel : INotifyPropertyChanged;

    public interface ISettingsPageModel : INotifyPropertyChanged;

    public interface IDetailPageModel : INotifyPropertyChanged;

    public interface IDeepDetailPageModel : INotifyPropertyChanged;

    private class HomePage : ContentPage
    {
        public HomePage(IHomePageModel model)
        {
            BindingContext = model;
        }
    }

    private class SearchPage : ContentPage
    {
        public SearchPage(ISearchPageModel model)
        {
            BindingContext = model;
        }
    }

    private class SettingsPage : ContentPage
    {
        public SettingsPage(ISettingsPageModel model)
        {
            BindingContext = model;
        }
    }

    private class DetailPage : ContentPage
    {
        public DetailPage(IDetailPageModel model)
        {
            BindingContext = model;
        }
    }

    private class DeepDetailPage : ContentPage
    {
        public DeepDetailPage(IDeepDetailPageModel model)
        {
            BindingContext = model;
        }
    }

    private sealed class RecordingPresenter : IScaffoldPresenter
    {
        public List<(ScaffoldRoot Root, ScaffoldPresentationHint Hint)> Syncs { get; } = [];

        public Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint)
        {
            Syncs.Add((root, hint));

            return Task.CompletedTask;
        }

        public Task<bool> ShowOverlayAsync(ScaffoldOverlayRequest request) => Task.FromResult(true);

        public Task ReplaceTabBarPanelAsync(ScaffoldOverlayRequest replacement) => Task.CompletedTask;

        public Task CloseOverlayAsync(ScaffoldOverlayRequest request)
        {
            request.Cleanup?.Invoke();

            return Task.CompletedTask;
        }

        public Task CloseTopOverlayAsync() => Task.CompletedTask;

        public Task CloseAllOverlaysAsync() => Task.CompletedTask;

        public bool HasOverlay => false;

        public bool IsOverlayPresented(ScaffoldOverlayRequest request) => false;

        public void ReleasePage(Page page)
        {
        }
    }

    private readonly ServiceProvider _serviceProvider;
    private readonly NavigationService _navigationService;
    private readonly RecordingPresenter _presenter = new();
    private readonly Scaffold _scaffold;
    private readonly ScaffoldTabBar _tabBar;

    public ScaffoldProxyTests()
    {
        // Typed bindings dispatch cross-thread property changes: the chrome-binding tests
        // update bound labels from context changes and need a dispatcher to exist.
        DispatcherProvider.SetCurrent(new DispatcherProviderStub());

        var services = new ServiceCollection();
        services.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
        services.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());

        var configurator = new NavigationConfigurator(services);
        var mapping = (IDictionary<Type, Type>) configurator.Mapping;
        mapping.Add(typeof(IHomePageModel), typeof(HomePage));
        mapping.Add(typeof(ISearchPageModel), typeof(SearchPage));
        mapping.Add(typeof(ISettingsPageModel), typeof(SettingsPage));
        mapping.Add(typeof(IDetailPageModel), typeof(DetailPage));
        mapping.Add(typeof(IDeepDetailPageModel), typeof(DeepDetailPage));

        services.AddScoped(_ => Substitute.For<IHomePageModel, IEnteringAware<ScaffoldStartupIntent>>());
        services.AddScoped<HomePage>();
        services.AddScoped(_ => Substitute.For<ISearchPageModel>());
        services.AddScoped<SearchPage>();
        services.AddScoped(_ => Substitute.For<ISettingsPageModel>());
        services.AddScoped<SettingsPage>();
        services.AddScoped(_ => Substitute.For<IDetailPageModel>());
        services.AddScoped<DetailPage>();
        services.AddScoped(_ => Substitute.For<IDeepDetailPageModel>());
        services.AddScoped<DeepDetailPage>();

        services.AddSingleton<INavigationService, NavigationService>();
        _serviceProvider = services.BuildServiceProvider();
        _navigationService = (NavigationService) _serviceProvider.GetRequiredService<INavigationService>();

        _tabBar = new ScaffoldTabBar
        {
            Roots =
            {
                new ScaffoldRoot { PageType = typeof(HomePage) },
                new ScaffoldRoot { PageType = typeof(SearchPage) }
            }
        };

        _scaffold = new Scaffold
        {
            Areas =
            {
                _tabBar,
                new ScaffoldRoot { PageType = typeof(SettingsPage) }
            },
            Presenter = _presenter
        };
    }

    [Fact(DisplayName = "Scaffold, when initialized, selects the first root and creates its page")]
    public async Task ScaffoldWhenInitializedSelectsTheFirstRootAndCreatesItsPage()
    {
        await _scaffold.InitializeAsync(_serviceProvider);

        _scaffold.CurrentArea.Should().Be(_tabBar);
        var homeRoot = _tabBar.Roots[0];
        _tabBar.CurrentRoot.Should().Be(homeRoot);
        homeRoot.IsSelected.Should().BeTrue();
        _tabBar.IsSelected.Should().BeTrue();
        homeRoot.NavigationStack.RootPage.Should().BeOfType<HomePage>();

        // Initial display is the handler's synchronization; no navigation commit happened.
        _presenter.Syncs.Should().BeEmpty();
        _scaffold.Proxy!.Location.Should().Be("//area0/HomePage");
    }

    [Fact(DisplayName = "Scaffold.NavBarContext forwards to the CURRENT page's own context")]
    public async Task ScaffoldNavBarContextForwardsToTheCurrentPage()
    {
        await _scaffold.InitializeAsync(_serviceProvider);

        var homePage = _tabBar.Roots[0].NavigationStack.RootPage!;
        var homeContext = _scaffold.NavBarContext;
        homeContext.Should().BeSameAs(_scaffold.GetPageHost(homePage)!.Context);

        var forwarderChanges = 0;
        _scaffold.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Scaffold.NavBarContext))
            {
                forwarderChanges++;
            }
        };

        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());

        var detailPage = _scaffold.CurrentPage!;
        detailPage.Should().NotBeSameAs(homePage);

        _scaffold.NavBarContext.Should().BeSameAs(_scaffold.GetPageHost(detailPage)!.Context);
        _scaffold.NavBarContext.Should().NotBeSameAs(homeContext, "each page owns its context");
        forwarderChanges.Should().BeGreaterThan(0, "bindings routed through the forwarder must re-evaluate");

        // The covered page keeps its own host and context while it stays in the stack.
        _scaffold.GetPageHost(homePage).Should().NotBeNull();
        _scaffold.GetPageHost(homePage)!.Context.Should().BeSameAs(homeContext);

        await _navigationService.GoToAsync(Navigation.Relative().Pop());

        _scaffold.NavBarContext.Should().BeSameAs(homeContext, "the revealed page's context is restored, not rebuilt");
        _scaffold.GetPageHost(detailPage).Should().BeNull("a popped page's host is disposed");
    }

    [Fact(DisplayName = "Scaffold, when initialized with InitialRootPageType, selects that root")]
    public async Task ScaffoldWhenInitializedWithInitialRootPageTypeSelectsThatRoot()
    {
        _scaffold.InitialRootPageType = typeof(SettingsPage);

        await _scaffold.InitializeAsync(_serviceProvider);

        var settingsArea = _scaffold.Areas[1];
        _scaffold.CurrentArea.Should().Be(settingsArea);
        settingsArea.CurrentRoot!.IsSelected.Should().BeTrue();
        settingsArea.CurrentRoot.NavigationStack.RootPage.Should().BeOfType<SettingsPage>();
        _tabBar.Roots[0].IsSelected.Should().BeFalse();
    }

    [Fact(DisplayName = "Scaffold, when initialized with InitialIntent, delivers it to the root page model")]
    public async Task ScaffoldWhenInitializedWithInitialIntentDeliversItToTheRootPageModel()
    {
        var intent = new ScaffoldStartupIntent();
        _scaffold.InitialIntent = intent;

        await _scaffold.InitializeAsync(_serviceProvider);

        var model = (IEnteringAware<ScaffoldStartupIntent>)_tabBar.Roots[0].NavigationStack.RootPage!.BindingContext;
        await model.Received(1).OnEnteringAsync(intent);
    }

    [Fact(DisplayName = "Scaffold CurrentPage tracks initialization, pushes, pops and area switches")]
    public async Task ScaffoldCurrentPageTracksNavigation()
    {
        _scaffold.CurrentPage.Should().BeNull("nothing is presented before initialization");

        await _scaffold.InitializeAsync(_serviceProvider);
        _scaffold.CurrentPage.Should().BeOfType<HomePage>();
        ((IPageContainer<Page>)_scaffold).CurrentPage.Should().BeSameAs(_scaffold.CurrentPage);

        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>());
        _scaffold.CurrentPage.Should().BeOfType<DeepDetailPage>();

        await _navigationService.GoToAsync(Navigation.Relative().Pop());
        _scaffold.CurrentPage.Should().BeOfType<DetailPage>();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<ISettingsPageModel>());
        _scaffold.CurrentPage.Should().BeOfType<SettingsPage>();

        await _navigationService.GoToAsync(Navigation.Absolute().Root<IHomePageModel>());
        _scaffold.CurrentPage.Should().BeOfType<HomePage>("returning to the preserved home stack");
    }

    [Fact(DisplayName = "Scaffold, when pushing multiple pages in one navigation, synchronizes the presenter once")]
    public async Task ScaffoldWhenPushingMultiplePagesInOneNavigationSynchronizesThePresenterOnce()
    {
        await _scaffold.InitializeAsync(_serviceProvider);
        _presenter.Syncs.Clear();

        var result = await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>());

        result.Should().BeTrue();
        var homeRoot = _tabBar.Roots[0];

        _presenter.Syncs.Should().ContainSingle()
                  .Which.Should().Be((homeRoot, ScaffoldPresentationHint.Push));

        var stack = homeRoot.NavigationStack;
        stack.PushedPages.Should().HaveCount(2);
        stack.PushedPages[0].Page.Should().BeOfType<DetailPage>();
        stack.PushedPages[1].Page.Should().BeOfType<DeepDetailPage>();
        stack.PushedPages[1].Route.Should().Be("//area0/HomePage/DetailPage/DeepDetailPage");
        _scaffold.Proxy!.Location.Should().Be("//area0/HomePage/DetailPage/DeepDetailPage");
        _scaffold.Proxy.State.Should().Be("//HomePage/DetailPage/DeepDetailPage");
    }

    [Fact(DisplayName = "Scaffold, when popping a page, synchronizes the presenter once with the pop hint")]
    public async Task ScaffoldWhenPoppingAPageSynchronizesThePresenterOnceWithThePopHint()
    {
        await _scaffold.InitializeAsync(_serviceProvider);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        _presenter.Syncs.Clear();

        var result = await _navigationService.GoToAsync(Navigation.Relative().Pop());

        result.Should().BeTrue();
        var homeRoot = _tabBar.Roots[0];

        _presenter.Syncs.Should().ContainSingle()
                  .Which.Should().Be((homeRoot, ScaffoldPresentationHint.Pop));

        homeRoot.NavigationStack.PushedPages.Should().BeEmpty();
        _scaffold.Proxy!.Location.Should().Be("//area0/HomePage");
    }

    [Fact(DisplayName = "Scaffold, when navigating to another area, updates selection and clears the left stack")]
    public async Task ScaffoldWhenNavigatingToAnotherAreaUpdatesSelectionAndClearsTheLeftStack()
    {
        await _scaffold.InitializeAsync(_serviceProvider);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        _presenter.Syncs.Clear();

        var result = await _navigationService.GoToAsync(Navigation.Absolute().Root<ISettingsPageModel>());

        result.Should().BeTrue();
        var settingsArea = _scaffold.Areas[1];
        var homeRoot = _tabBar.Roots[0];

        _scaffold.CurrentArea.Should().Be(settingsArea);
        settingsArea.IsSelected.Should().BeTrue();
        settingsArea.CurrentRoot!.IsSelected.Should().BeTrue();
        _tabBar.IsSelected.Should().BeFalse();
        homeRoot.IsSelected.Should().BeFalse();

        // Default behavior pops all pages when leaving the item.
        homeRoot.NavigationStack.PushedPages.Should().BeEmpty();

        _presenter.Syncs.Should().NotBeEmpty();
        _presenter.Syncs[^1].Root.Should().Be(settingsArea.CurrentRoot);

        // Settings lives in ANOTHER area: the two roots share no strip to travel along, so the
        // switch cross-fades instead of sliding.
        _presenter.Syncs[^1].Hint.Should().Be(ScaffoldPresentationHint.Fade);
        _scaffold.Proxy!.Location.Should().Be("//area1/SettingsPage");
    }

    [Fact(DisplayName = "Scaffold, when navigating between tabs of the same area, preserves the previous tab stack")]
    public async Task ScaffoldWhenNavigatingBetweenTabsOfTheSameAreaPreservesThePreviousTabStack()
    {
        await _scaffold.InitializeAsync(_serviceProvider);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        _presenter.Syncs.Clear();

        var result = await _navigationService.GoToAsync(
            Navigation.Absolute(NavigationBehavior.None).Root<ISearchPageModel>()
        );

        result.Should().BeTrue();
        var homeRoot = _tabBar.Roots[0];
        var searchRoot = _tabBar.Roots[1];

        _tabBar.CurrentRoot.Should().Be(searchRoot);
        searchRoot.IsSelected.Should().BeTrue();
        homeRoot.IsSelected.Should().BeFalse();

        // NavigationBehavior.None: the home stack is preserved for when the user returns.
        homeRoot.NavigationStack.PushedPages.Should().ContainSingle();
        homeRoot.NavigationStack.RootPage.Should().NotBeNull();
    }

    [Fact(DisplayName = "Scaffold, when disposed, releases all pages and DI scopes")]
    public async Task ScaffoldWhenDisposedReleasesAllPagesAndDiScopes()
    {
        await _scaffold.InitializeAsync(_serviceProvider);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        var homeRoot = _tabBar.Roots[0];

        _scaffold.Dispose();

        homeRoot.NavigationStack.RootPage.Should().BeNull();
        homeRoot.NavigationStack.PushedPages.Should().BeEmpty();
    }

    [Fact(DisplayName = "Chrome outside any page (the tab bar) follows the PRESENTED page's context")]
    public async Task ChromeOutsideAnyPageFollowsThePresentedPage()
    {
        await _scaffold.InitializeAsync(_serviceProvider);

        // The default tab bar view: a logical child of the ScaffoldTabBar area — inside the
        // scaffold's tree but under NO page, exactly like a scroll-driven binding on it (the
        // hide-on-scroll tab bar chrome).
        var barView = (ScaffoldTabBarView)_tabBar.GetOrCreateBarView();
        var chrome = new Label();
        barView.Add(chrome);
        chrome.SetBinding(Label.TextProperty, NavBarBindings.Create(chrome, nameof(ScaffoldNavBarContext.Title)));

        _tabBar.Roots[0].NavigationStack.RootPage!.Title = "Home";
        chrome.Text.Should().Be("Home");

        await _navigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        _scaffold.CurrentPage!.Title = "Detail";
        chrome.Text.Should().Be("Detail", "a navigation re-points the scaffold-level context with NO ancestry change — the relay must follow it");

        await _navigationService.GoToAsync(Navigation.Relative().Pop());
        chrome.Text.Should().Be("Home", "popping restores the previous page's context");
    }
}
