namespace Nalu.Maui.Test.NavigationTests;

#pragma warning disable CA2012,CS4014,VSTHRD110

public partial class NavigationServiceTests
{
    private TestComponentPageFactory? _componentPageFactory;
    private bool _registerComponentPageFactory = true;

    public class ComponentA : IEnteringAware, IEnteringAware<OddIntent>, IAppearingAware, IAppearingAware<OddIntent>, IDisappearingAware, ILeavingAware, IDisposable
    {
        // Constructor injection from the page's navigation scope is part of the contract.
        public ComponentA(INavigationServiceProvider navigationServiceProvider)
            => navigationServiceProvider.Should().NotBeNull();

        public List<string> Events { get; } = [];

        public ValueTask OnEnteringAsync()
        {
            Events.Add("Entering");

            return ValueTask.CompletedTask;
        }

        public ValueTask OnEnteringAsync(OddIntent intent)
        {
            Events.Add($"Entering({intent.Value})");

            return ValueTask.CompletedTask;
        }

        public ValueTask OnAppearingAsync()
        {
            Events.Add("Appearing");

            return ValueTask.CompletedTask;
        }

        public ValueTask OnAppearingAsync(OddIntent intent)
        {
            Events.Add($"Appearing({intent.Value})");

            return ValueTask.CompletedTask;
        }

        public ValueTask OnDisappearingAsync()
        {
            Events.Add("Disappearing");

            return ValueTask.CompletedTask;
        }

        public ValueTask OnLeavingAsync()
        {
            Events.Add("Leaving");

            return ValueTask.CompletedTask;
        }

        public void Dispose() => Events.Add("ScopeDisposed");
    }

    public class GuardedComponent : ILeavingGuard
    {
        public bool CanLeaveResult { get; set; }

        public ValueTask<bool> CanLeaveAsync() => ValueTask.FromResult(CanLeaveResult);
    }

    internal sealed class TestComponentPageHandle(Page page, object component) : IComponentPageHandle
    {
        public bool Disposed { get; private set; }
        public Page Page { get; } = page;
        public object LifecycleTarget { get; } = component;

        public void Dispose()
        {
            Disposed = true;
            (LifecycleTarget as ComponentA)?.Events.Add("HandleDisposed");
        }
    }

    internal sealed class TestComponentPageFactory : IComponentPageFactory
    {
        public List<TestComponentPageHandle> Handles { get; } = [];

        public IComponentPageHandle CreatePage(object component)
        {
            var handle = new TestComponentPageHandle(new ContentPage(), component);
            Handles.Add(handle);

            return handle;
        }
    }

    private void ConfigureComponentPages(IServiceCollection serviceCollection)
    {
        var configurator = (NavigationConfigurator) _navigationConfiguration;
        configurator.AddPage<ComponentA>();
        configurator.AddPage<GuardedComponent>();

        if (_registerComponentPageFactory)
        {
            _componentPageFactory = new TestComponentPageFactory();
            serviceCollection.AddSingleton<IComponentPageFactory>(_componentPageFactory);
        }
    }

    [Fact(DisplayName = "AddPage with a non-page type should register a component page, not a view-only page")]
    public void AddPageWithANonPageTypeShouldRegisterAComponentPage()
    {
        var serviceCollection = new ServiceCollection();
        var configurator = new NavigationConfigurator(serviceCollection);

        configurator.AddPage<ComponentA>().AddPage<ComponentA>();

        configurator.ComponentPages.Should().BeEquivalentTo([typeof(ComponentA)]);
        configurator.ViewOnlyPages.Should().BeEmpty();
        configurator.Mapping.Should().BeEmpty();
        serviceCollection.Count(descriptor => descriptor.ServiceType == typeof(ComponentA)).Should().Be(1);

        NavigationHelper.GetPageType(typeof(ComponentA), configurator).Should().Be(typeof(ComponentA));
    }

    [Fact(DisplayName = "NavigationService, when pushing a component page, should create the page via the factory and dispatch lifecycle to the component")]
    public async Task NavigationServiceWhenPushingAComponentPageShouldDispatchLifecycleToTheComponent()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        await _navigationService.GoToAsync(Navigation.Relative().Push<ComponentA>());

        var handle = _componentPageFactory!.Handles.Should().ContainSingle().Subject;
        var component = (ComponentA) handle.LifecycleTarget;

        component.Events.Should().Equal("Entering", "Appearing");
        NavigationHelper.GetLifecycleTarget(handle.Page).Should().BeSameAs(component);

        // The page's BindingContext stays untouched: no context propagation through the
        // component-rendered tree.
        handle.Page.IsSet(BindableObject.BindingContextProperty).Should().BeFalse();
    }

    [Fact(DisplayName = "NavigationService, when pushing a component page with intent, should invoke the typed lifecycle methods")]
    public async Task NavigationServiceWhenPushingAComponentPageWithIntentShouldInvokeTheTypedLifecycleMethods()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        await _navigationService.GoToAsync(Navigation.Relative().Push<ComponentA>().WithIntent(new OddIntent("X")));

        var component = (ComponentA) _componentPageFactory!.Handles.Single().LifecycleTarget;
        component.Events.Should().Equal("Entering(X)", "Appearing(X)");
    }

    [Fact(DisplayName = "NavigationService, when popping a component page, should dispatch leaving lifecycle then dispose the handle before the scope")]
    public async Task NavigationServiceWhenPoppingAComponentPageShouldDisposeTheHandleBeforeTheScope()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<ComponentA>());

        var handle = _componentPageFactory!.Handles.Single();
        var component = (ComponentA) handle.LifecycleTarget;

        await _navigationService.GoToAsync(Navigation.Relative().Pop());

        handle.Disposed.Should().BeTrue();
        component.Events.Should().Equal("Entering", "Appearing", "Disappearing", "Leaving", "HandleDisposed", "ScopeDisposed");
        PageNavigationContext.TryGet(handle.Page).Should().BeNull();
    }

    [Fact(DisplayName = "NavigationService, when popping a guarded component page, should honor the component's leaving guard")]
    public async Task NavigationServiceWhenPoppingAGuardedComponentPageShouldHonorTheLeavingGuard()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<GuardedComponent>());

        var handle = _componentPageFactory!.Handles.Single();
        var component = (GuardedComponent) handle.LifecycleTarget;

        component.CanLeaveResult = false;
        (await _navigationService.GoToAsync(Navigation.Relative().Pop())).Should().BeFalse();
        handle.Disposed.Should().BeFalse();

        component.CanLeaveResult = true;
        (await _navigationService.GoToAsync(Navigation.Relative().Pop())).Should().BeTrue();
        handle.Disposed.Should().BeTrue();
    }

    [Fact(DisplayName = "GetLifecycleTarget, on a component page, should return the component even when a binding context is explicitly set")]
    public async Task GetLifecycleTargetOnAComponentPageShouldReturnTheComponentEvenWhenABindingContextIsExplicitlySet()
    {
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);
        await _navigationService.GoToAsync(Navigation.Relative().Push<ComponentA>());

        var handle = _componentPageFactory!.Handles.Single();
        handle.Page.BindingContext = new object();

        NavigationHelper.GetLifecycleTarget(handle.Page).Should().BeSameAs(handle.LifecycleTarget);
    }

    [Fact(DisplayName = "NavigationService, when pushing a component page without a factory, should throw a descriptive error")]
    public async Task NavigationServiceWhenPushingAComponentPageWithoutAFactoryShouldThrow()
    {
        _registerComponentPageFactory = false;
        ConfigureTestAsync("c1");
        await _navigationService.InitializeAsync(_shellProxy, nameof(Page1), null);

        var navigate = async () => await _navigationService.GoToAsync(Navigation.Relative().Push<ComponentA>());

        await navigate.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{nameof(IComponentPageFactory)}*");
    }
}
