namespace Nalu.Maui.Test.Navigation;

using System.ComponentModel;
using Navigation = Nalu.Navigation;

#pragma warning disable CA2012
#pragma warning disable VSTHRD110

public class NavigationServiceTests
{
    public record AnIntent(int Value = 0);

    public interface ISomeContext;

    public interface INavigationAwareTest<in T> :
        INotifyPropertyChanged,
        IEnteringAware, IEnteringAware<T>,
        IAppearingAware, IAppearingAware<T>,
        IDisappearingAware,
        ILeavingAware,
        IDisposable;

    public interface IOneTestPageModel : INavigationAwareTest<AnIntent>;

    public interface ITwoTestPageModel : INavigationAwareTest<AnIntent>;

    public interface IThreeTestPageModel : INavigationAwareTest<string>;

    public interface IGuardedTestPageModel : INavigationAwareTest<AnIntent>, ILeavingGuard;

    public interface IRealTestPageModel : INotifyPropertyChanged;

    private class BaseTestPage : ContentPage
    {
        protected BaseTestPage(object model)
        {
            BindingContext = model;
        }
    }

    private class RealTestPageModel : IRealTestPageModel
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }

    private class RealTestNavigationController : INavigationController
    {
        public IReadOnlyList<Page> NavigationStack { get; private set; } = [];
        public Task<T> ExecuteNavigationAsync<T>(Func<Task<T>> navigationFunc) => navigationFunc();

        public Page CurrentPage => NavigationStack[^1];
        public Page RootPage => NavigationStack[0];

        public Task SetRootPageAsync(Page page)
        {
            NavigationStack = new[] { page };
            return Task.CompletedTask;
        }

        public void ConfigurePage(Page page)
        {
        }

        public Task PopAsync(int times = 1)
        {
            NavigationStack = NavigationStack.Take(NavigationStack.Count - times).ToList();
            return Task.CompletedTask;
        }

        public Task PushAsync(Page page)
        {
            NavigationStack = NavigationStack.Concat(new[] { page }).ToList();
            return Task.CompletedTask;
        }
    }

    private class RealTestPage(IRealTestPageModel model) : BaseTestPage(model);

    private class OneTestPage(IOneTestPageModel model) : BaseTestPage(model);

    private class TwoTestPage(ITwoTestPageModel model) : BaseTestPage(model);

    private class ThreeTestPage(IThreeTestPageModel model) : BaseTestPage(model);

    private class GuardedTestPage(IGuardedTestPageModel model) : BaseTestPage(model);

    private readonly INavigationServiceInternal _navigationService;
    private readonly INavigationController _navigationController;
    private readonly ServiceProvider _serviceProvider;
    private readonly INavigationOptions _navigationOptions;

    public NavigationServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
        serviceCollection.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());
        _navigationOptions = new NavigationConfigurator(serviceCollection, typeof(NavigationServiceTests));
        var mapping = (IDictionary<Type, Type>)_navigationOptions.Mapping;

        mapping.Add(typeof(IRealTestPageModel), typeof(RealTestPage));
        mapping.Add(typeof(IOneTestPageModel), typeof(OneTestPage));
        mapping.Add(typeof(ITwoTestPageModel), typeof(TwoTestPage));
        mapping.Add(typeof(IThreeTestPageModel), typeof(ThreeTestPage));
        mapping.Add(typeof(IGuardedTestPageModel), typeof(GuardedTestPage));

        serviceCollection.AddScoped<IRealTestPageModel, RealTestPageModel>();
        serviceCollection.AddScoped<RealTestPage>();
        serviceCollection.AddScoped<IOneTestPageModel>(_ => Substitute.For<IOneTestPageModel>());
        serviceCollection.AddScoped<OneTestPage>();
        serviceCollection.AddScoped<ITwoTestPageModel>(_ => Substitute.For<ITwoTestPageModel>());
        serviceCollection.AddScoped<TwoTestPage>();
        serviceCollection.AddScoped<IThreeTestPageModel>(_ => Substitute.For<IThreeTestPageModel>());
        serviceCollection.AddScoped<ThreeTestPage>();
        serviceCollection.AddScoped<IGuardedTestPageModel>(_ => Substitute.For<IGuardedTestPageModel>());
        serviceCollection.AddScoped<GuardedTestPage>();

        _serviceProvider = serviceCollection.BuildServiceProvider();
        _navigationService = new NavigationService(_serviceProvider, _navigationOptions);

        var navigationStack = new List<Page>();
        _navigationController = Substitute.For<INavigationController>();
        _navigationController.NavigationStack.Returns(navigationStack);
        _navigationController.RootPage.Returns(_ => navigationStack[0]);
        _navigationController.CurrentPage.Returns(_ => navigationStack[^1]);
        _navigationController.ExecuteNavigationAsync(Arg.Any<Func<Task<bool>>>())
            .Returns(callInfo => callInfo.Arg<Func<Task<bool>>>()());
        _navigationController
            .When(m => m.SetRootPageAsync(Arg.Any<Page>()))
            .Do(callInfo =>
            {
                var rootPage = callInfo.Arg<Page>();
                navigationStack.Clear();
                navigationStack.Add(rootPage);
            });
        _navigationController
            .When(m => m.PushAsync(Arg.Any<Page>()))
            .Do(callInfo =>
            {
                var page = callInfo.Arg<Page>();
                navigationStack.Add(page);
            });
        _navigationController
            .When(m => m.PopAsync(Arg.Any<int>()))
            .Do(callInfo =>
            {
                var times = callInfo.Arg<int>();
                navigationStack.RemoveRange(navigationStack.Count - times, times);
            });
    }

    [Fact(DisplayName = "NavigationService, when initialized, should set the root page, sending entering and appearing")]
    public void NavigationServiceWhenInitializedShouldSetTheRootPageSendingEnteringAndAppearing()
    {
        _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);

        _navigationController.Received().SetRootPageAsync(Arg.Any<OneTestPage>());
        var page = _navigationController.RootPage;
        var model = (IOneTestPageModel)page.BindingContext;

        Received.InOrder(() =>
        {
            model.OnEnteringAsync();
            model.OnAppearingAsync();
        });
        model.DidNotReceive().OnEnteringAsync(null!);
        model.DidNotReceive().OnAppearingAsync(null!);
    }

    [Fact(DisplayName = "NavigationService, when initialized with intent, should set the root page, sending entering and appearing with intent")]
    public void NavigationServiceWhenInitializedWithIntentShouldSetTheRootPageSendingEnteringAndAppearingWithIntent()
    {
        var intent = new AnIntent();
        _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController, intent);

        _navigationController.Received().SetRootPageAsync(Arg.Any<OneTestPage>());
        var page = _navigationController.RootPage;
        var model = (IOneTestPageModel)page.BindingContext;

        Received.InOrder(() =>
        {
            model.OnEnteringAsync(intent);
            model.OnAppearingAsync(intent);
        });
        model.DidNotReceive().OnEnteringAsync();
        model.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when initialized with incorrect intent, should throw")]
    public void NavigationServiceWhenInitializedWithIncorrectIntentShouldThrow()
    {
        var intent = "an intent";

        var initializeAction = () => _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController, intent);

        initializeAction.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "NavigationService, when pushing a page, should send disappearing on current page, then set the new page, sending entering and appearing")]
    public async Task NavigationServiceWhenPushingAPageShouldSendDisappearingOnCurrentPageThenSetTheNewPageSendingEnteringAndAppearing()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.RootPage;
        var model1 = (IOneTestPageModel)page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());

        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;

        Received.InOrder(() =>
        {
            model1.OnDisappearingAsync();
            model2.OnEnteringAsync();
            _navigationController.PushAsync(page2);
            model2.OnAppearingAsync();
        });
        _ = model1.DidNotReceive().OnLeavingAsync();
        _ = model2.DidNotReceive().OnEnteringAsync(null!);
        _ = model2.DidNotReceive().OnAppearingAsync(null!);
    }

    [Fact(DisplayName = "NavigationService, when pushing a page with intent, should send disappearing on current page, then set the new page, sending entering and appearing")]
    public async Task NavigationServiceWhenPushingAPageWithIntentShouldSendDisappearingOnCurrentPageThenSetTheNewPageSendingEnteringAndAppearing()
    {
        var intent = new AnIntent();
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.RootPage;
        var model1 = (IOneTestPageModel)page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative(intent).Push<ITwoTestPageModel>());

        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;

        Received.InOrder(() =>
        {
            model1.OnDisappearingAsync();
            model2.OnEnteringAsync(intent);
            _navigationController.PushAsync(page2);
            model2.OnAppearingAsync(intent);
        });
        _ = model1.DidNotReceive().OnLeavingAsync();
        _ = model2.DidNotReceive().OnEnteringAsync();
        _ = model2.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when pushing a page with incorrect intent, should throw")]
    public async Task NavigationServiceWhenPushingAPageWithIncorrectIntentShouldThrow()
    {
        var intent = "an intent";
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.RootPage;
        var model1 = (IOneTestPageModel)page1.BindingContext;
        model1.ClearReceivedCalls();

        var pushAction = () => _navigationService.GoToAsync(Navigation.Relative(intent).Push<ITwoTestPageModel>());

        await pushAction.Should().ThrowAsync<InvalidOperationException>();

        _ = model1.DidNotReceive().OnDisappearingAsync();
        _ = model1.DidNotReceive().OnLeavingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a page, should send disappearing and leaving on current page, then pop sending appearing")]
    public async Task NavigationServiceWhenPoppingAPageShouldSendDisappearingAndLeavingOnCurrentPageThenPopSendingAppearing()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());

        var page1 = _navigationController.NavigationStack[^2];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Pop());

        Received.InOrder(() =>
        {
            model2.OnDisappearingAsync();
            model2.OnLeavingAsync();
            _navigationController.PopAsync(1);
            model1.OnAppearingAsync();
        });
        _ = model1.DidNotReceive().OnEnteringAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a page, should not leak the popped page")]
    public async Task NavigationServiceWhenPoppingAPageShouldNotLeakThePoppedPage()
    {
        // I would like to use _navigationController here, but NSubstitute leaks, even when clearing received calls
        // See: https://github.com/nsubstitute/NSubstitute/issues/771
        var navigationService = new NavigationService(_serviceProvider, _navigationOptions);
        var navigationController = new RealTestNavigationController();
        WeakReference<Page> weakPage;
        {
            await ((INavigationServiceInternal)navigationService).InitializeAsync<IOneTestPageModel>(navigationController);
            await navigationService.GoToAsync(Navigation.Relative().Push<IRealTestPageModel>());
            weakPage = new WeakReference<Page>(navigationController.CurrentPage);
            await navigationService.GoToAsync(Navigation.Relative().Pop());
        }

        await Task.Yield();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        weakPage.TryGetTarget(out _).Should().BeFalse();
    }

    [Fact(DisplayName = "NavigationService, when popping a page with intent, should send disappearing and leaving on current page, then pop sending appearing")]
    public async Task NavigationServiceWhenPoppingAPageWithIntentShouldSendDisappearingAndLeavingOnCurrentPageThenPopSendingAppearing()
    {
        var intent = new AnIntent();
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());

        var page1 = _navigationController.NavigationStack[^2];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative(intent).Pop());

        Received.InOrder(() =>
        {
            model2.OnDisappearingAsync();
            model2.OnLeavingAsync();
            _navigationController.PopAsync(1);
            model2.Dispose();
            model1.OnAppearingAsync(intent);
        });
        _ = model1.DidNotReceive().OnEnteringAsync(intent);
        _ = model1.DidNotReceive().OnEnteringAsync();
        _ = model1.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a page with incorrect intent, should throw")]
    public async Task NavigationServiceWhenPoppingAPageWithIncorrectIntentShouldThrow()
    {
        const string intent = "an intent";
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());

        var page1 = _navigationController.NavigationStack[^2];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        var popAction = () => _navigationService.GoToAsync(Navigation.Relative(intent).Pop());

        await popAction.Should().ThrowAsync<InvalidOperationException>();

        _ = model2.DidNotReceive().OnDisappearingAsync();
        _ = model2.DidNotReceive().OnLeavingAsync();
        _ = model1.DidNotReceive().OnEnteringAsync();
        _ = model1.DidNotReceive().OnAppearingAsync();
        _ = model1.DidNotReceive().OnEnteringAsync();
        _ = model1.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping multiple pages, should send disappearing only to current page and appearing to target page")]
    public async Task NavigationServiceWhenPoppingMultiplePagesShouldSendDisappearingOnlyToCurrentPageAndAppearingToTargetPage()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());
        await _navigationService.GoToAsync(Navigation.Relative().Push<IThreeTestPageModel>());

        var currentPage = _navigationController.NavigationStack[^1];
        var currentModel = (IThreeTestPageModel)currentPage.BindingContext;
        var midPage = _navigationController.NavigationStack[^2];
        var midModel = (ITwoTestPageModel)midPage.BindingContext;
        var targetPage = _navigationController.NavigationStack[^3];
        var targetModel = (IOneTestPageModel)targetPage.BindingContext;
        currentModel.ClearReceivedCalls();
        targetModel.ClearReceivedCalls();
        midModel.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Pop().Pop());

        Received.InOrder(() =>
        {
            currentModel.OnDisappearingAsync();
            currentModel.OnLeavingAsync();
            midModel.OnLeavingAsync();
            _navigationController.PopAsync(2);
            currentModel.Dispose();
            midModel.Dispose();
            targetModel.OnAppearingAsync();
        });
        _ = targetModel.DidNotReceive().OnEnteringAsync();
        _ = midModel.DidNotReceive().OnAppearingAsync();
        _ = midModel.DidNotReceive().OnDisappearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a guarded page, evaluates guard and cancels if false")]
    public async Task NavigationServiceWhenPoppingAGuardedPageEvaluatesGuardAndCancelsIfFalse()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IGuardedTestPageModel>());

        var page1 = _navigationController.NavigationStack[^2];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (IGuardedTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        model2.CanLeaveAsync().Returns(ValueTask.FromResult(false));

        var navigatedToTarget = await _navigationService.GoToAsync(Navigation.Relative().Pop());

        navigatedToTarget.Should().BeFalse();

        _ = model2.Received().CanLeaveAsync();
        _ = model2.DidNotReceive().OnDisappearingAsync();
        _ = model2.DidNotReceive().OnLeavingAsync();
    }

    [Fact(DisplayName = "NavigationService, when popping a guarded page, evaluates guard and proceeds if true")]
    public async Task NavigationServiceWhenPoppingAGuardedPageEvaluatesGuardAndProceedsIfTrue()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        await _navigationService.GoToAsync(Navigation.Relative().Push<IGuardedTestPageModel>());

        var page1 = _navigationController.NavigationStack[^2];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        var page2 = _navigationController.NavigationStack[^1];
        var model2 = (IGuardedTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        model2.CanLeaveAsync().Returns(ValueTask.FromResult(true));

        var navigatedToTarget = await _navigationService.GoToAsync(Navigation.Relative().Pop());

        navigatedToTarget.Should().BeTrue();

        Received.InOrder(() =>
        {
            _ = model2.CanLeaveAsync();
            _ = model2.OnDisappearingAsync();
            _ = model2.OnLeavingAsync();
            _ = model1.OnAppearingAsync();
        });
    }

    [Fact(DisplayName = "NavigationService, when replacing root page, sends disappearing and leaving on current page, then sets the new page, sending entering and appearing")]
    public async Task NavigationServiceWhenReplacingRootPageSendsDisappearingAndLeavingOnCurrentPageThenSetsTheNewPageSendingEnteringAndAppearing()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.NavigationStack[0];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative().Pop().Push<ITwoTestPageModel>());
        var page2 = _navigationController.NavigationStack[0];
        var model2 = (ITwoTestPageModel)page2.BindingContext;

        Received.InOrder(() =>
        {
            _ = model1.OnDisappearingAsync();
            _ = model1.OnLeavingAsync();
            _ = model2.OnEnteringAsync();
            _ = model2.OnAppearingAsync();
        });
    }

    [Fact(DisplayName = "NavigationService, when replacing root page with intent, sends disappearing and leaving on current page, then sets the new page, sending entering and appearing")]
    public async Task NavigationServiceWhenReplacingRootPageWithIntentSendsDisappearingAndLeavingOnCurrentPageThenSetsTheNewPageSendingEnteringAndAppearing()
    {
        var intent = new AnIntent();
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.NavigationStack[0];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        model1.ClearReceivedCalls();

        await _navigationService.GoToAsync(Navigation.Relative(intent).Pop().Push<ITwoTestPageModel>());
        var page2 = _navigationController.NavigationStack[0];
        var model2 = (ITwoTestPageModel)page2.BindingContext;

        Received.InOrder(() =>
        {
            _ = model1.OnDisappearingAsync();
            _ = model1.OnLeavingAsync();
            _ = model2.OnEnteringAsync(intent);
            _navigationController.SetRootPageAsync(page2);
            model1.Dispose();
            _ = model2.OnAppearingAsync(intent);
        });
    }

    [Fact(DisplayName = "NavigationService, when replacing a stack with another one, sends events accordingly")]
    public async Task NavigationServiceWhenReplacingAStackWithAnotherOneSendsEventsAccordingly()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.NavigationStack[0];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());
        var page2 = _navigationController.NavigationStack[1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        await _navigationService.GoToAsync(
            Navigation.Relative()
                .Pop()
                .Pop()
                .Push<IThreeTestPageModel>()
                .Push<IGuardedTestPageModel>());

        var page3 = _navigationController.NavigationStack[0];
        var model3 = (IThreeTestPageModel)page3.BindingContext;
        var page4 = _navigationController.NavigationStack[1];
        var model4 = (IGuardedTestPageModel)page4.BindingContext;

        Received.InOrder(() =>
        {
            _ = model2.OnDisappearingAsync();
            _ = model2.OnLeavingAsync();
            model2.Dispose();
            _ = model1.OnLeavingAsync();
            _ = model3.OnEnteringAsync();
            model1.Dispose();
            _ = model4.OnEnteringAsync();
            _ = model4.OnAppearingAsync();
        });

        _ = model1.DidNotReceive().OnAppearingAsync();
        _ = model1.DidNotReceive().OnDisappearingAsync();
        _ = model3.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when replacing a stack with another one with intent, sends events accordingly")]
    public async Task NavigationServiceWhenReplacingAStackWithAnotherOneWithIntentSendsEventsAccordingly()
    {
        var intent = new AnIntent();

        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.NavigationStack[0];
        var model1 = (IOneTestPageModel)page1.BindingContext;
        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());
        var page2 = _navigationController.NavigationStack[1];
        var model2 = (ITwoTestPageModel)page2.BindingContext;
        model1.ClearReceivedCalls();
        model2.ClearReceivedCalls();

        await _navigationService.GoToAsync(
            Navigation.Relative(intent)
                .Pop()
                .Pop()
                .Push<IThreeTestPageModel>()
                .Push<IGuardedTestPageModel>());

        var page3 = _navigationController.NavigationStack[0];
        var model3 = (IThreeTestPageModel)page3.BindingContext;
        var page4 = _navigationController.NavigationStack[1];
        var model4 = (IGuardedTestPageModel)page4.BindingContext;

        Received.InOrder(() =>
        {
            _ = model2.OnDisappearingAsync();
            _ = model2.OnLeavingAsync();
            _ = model1.OnLeavingAsync();
            _ = model3.OnEnteringAsync();
            _ = model4.OnEnteringAsync(intent);
            _ = model4.OnAppearingAsync(intent);
        });

        _ = model1.DidNotReceive().OnAppearingAsync();
        _ = model1.DidNotReceive().OnDisappearingAsync();
        _ = model3.DidNotReceive().OnAppearingAsync();
    }

    [Fact(DisplayName = "NavigationService, when pushing a page, initialize the nested navigation service provider")]
    public async Task NavigationServiceWhenPushingAPageInitializeTheNestedNavigationServiceProvider()
    {
        await _navigationService.InitializeAsync<IOneTestPageModel>(_navigationController);
        var page1 = _navigationController.RootPage;
        var page1sp = PageNavigationContext.Get(page1).ServiceScope.ServiceProvider.GetRequiredService<INavigationServiceProvider>();
        var context = Substitute.For<ISomeContext>();
        page1sp.AddNavigationScoped(context);

        await _navigationService.GoToAsync(Navigation.Relative().Push<ITwoTestPageModel>());
        var page2 = _navigationController.CurrentPage;
        var page2sp = PageNavigationContext.Get(page2).ServiceScope.ServiceProvider.GetRequiredService<INavigationServiceProvider>();

        context.Should().BeSameAs(page2sp.GetRequiredService<ISomeContext>());
    }

    [Fact(DisplayName = "NavigationService, GoToAsync, can be easily testable")]
    public async Task NavigationServiceGoToAsyncCanBeEasilyTestable()
    {
        var navigationService = Substitute.For<INavigationService>();
        navigationService.GoToAsync(Arg.Any<Navigation>()).Returns(Task.FromResult(true));
        {
            // Simulate what would be the code to be tested
            var intent = new AnIntent(5);
            await navigationService.GoToAsync(Navigation.Relative(intent).Push<IOneTestPageModel>());
        }

        // Assert that the code did what it was supposed to do
        var expectedNavigation = Navigation.Relative(new AnIntent(5)).Push<IOneTestPageModel>();
        await navigationService.Received().GoToAsync(
            Arg.Is<Navigation>(n => n.Matches(expectedNavigation)));
    }
}
