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

        public Task OpenFlyoutAsync(ScaffoldFlyoutSide side, View content) => Task.CompletedTask;

        public Task OpenTabBarOverflowAsync(ScaffoldTabBar tabBar, ScaffoldTabBarView barView) => Task.CompletedTask;

        public bool HasOverlay => false;

        public Task CloseOverlayAsync() => Task.CompletedTask;
    }

    private readonly ServiceProvider _serviceProvider;
    private readonly NavigationService _navigationService;
    private readonly RecordingPresenter _presenter = new();
    private readonly Scaffold _scaffold;
    private readonly ScaffoldTabBar _tabBar;

    public ScaffoldProxyTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
        services.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());

        var configurator = new NavigationConfigurator(services, typeof(ScaffoldProxyTests));
        var mapping = (IDictionary<Type, Type>) configurator.Mapping;
        mapping.Add(typeof(IHomePageModel), typeof(HomePage));
        mapping.Add(typeof(ISearchPageModel), typeof(SearchPage));
        mapping.Add(typeof(ISettingsPageModel), typeof(SettingsPage));
        mapping.Add(typeof(IDetailPageModel), typeof(DetailPage));
        mapping.Add(typeof(IDeepDetailPageModel), typeof(DeepDetailPage));

        services.AddScoped(_ => Substitute.For<IHomePageModel>());
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

        // Root switches slide in the direction of travel: Settings sits AFTER the tab bar's
        // roots in the structure, so the new content enters from the end edge.
        _presenter.Syncs[^1].Hint.Should().Be(ScaffoldPresentationHint.SlideEnd);
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
}
