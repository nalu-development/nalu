using System.ComponentModel;
using System.Text.Json;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// Navigation-state snapshot &amp; restore tests: the REAL <see cref="NavigationRestoreService"/>
/// capturing from and replaying through the REAL engine, hosted by the REAL
/// <see cref="ScaffoldProxy"/> (restore is engine-level; the Scaffold is the verified host).
/// Round-trips run capture → in-memory store → a FRESH scaffold/engine pair (a new "process") → boot.
/// </summary>
public class NavigationRestoreTests
{
    static NavigationRestoreTests()
    {
        // The replay loop enqueues each step through the page dispatcher. In a FULL suite run
        // another test class happens to install the global stub first; a FILTERED run of this
        // class alone must not depend on that ordering.
        DispatcherProvider.SetCurrent(new DispatcherProviderStub());
    }

    public sealed record SearchIntent(string Value);

    public sealed record DetailIntent(string Value);

    public sealed record DeepDetailIntent(string Value);

    /// <summary>NOT registered via AddIntent: ends the restorable prefix.</summary>
    public sealed record OpaqueIntent(string Value);

    /// <summary>An intent whose heavy state is excluded from the snapshot and rehydrated at replay.</summary>
    public sealed class HydratableIntent
    {
        public string? Value { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public object? Heavy { get; set; }
    }

    public interface IHomePageModel : INotifyPropertyChanged, IAppearingAware, IIntentHydrator<HydratableIntent>;

    public interface ISearchPageModel : INotifyPropertyChanged, IEnteringAware<SearchIntent>;

    public interface IDetailPageModel : INotifyPropertyChanged, IEnteringAware<DetailIntent>, IEnteringAware<HydratableIntent>, IEnteringAware;

    public interface IDeepDetailPageModel : INotifyPropertyChanged, IEnteringAware<DeepDetailIntent>, IEnteringAware<OpaqueIntent>, IEnteringAware;

    private class HomePage : ContentPage
    {
        public HomePage(IHomePageModel model) => BindingContext = model;
    }

    private class SearchPage : ContentPage
    {
        public SearchPage(ISearchPageModel model) => BindingContext = model;
    }

    private class DetailPage : ContentPage
    {
        public DetailPage(IDetailPageModel model) => BindingContext = model;
    }

    private class DeepDetailPage : ContentPage
    {
        public DeepDetailPage(IDeepDetailPageModel model) => BindingContext = model;
    }

    // View-only registration (AddPage<TPage>()): no page model, never enters Mapping —
    // exercises the ViewOnlyPages segment-resolution path.
    private class ViewOnlyPage : ContentPage;

    private sealed class InMemoryStore : INavigationRestoreStore
    {
        public string? Stored { get; set; }

        public string? ReadAndDelete()
        {
            var payload = Stored;
            Stored = null;

            return payload;
        }

        public Task WriteAsync(string snapshot, CancellationToken cancellationToken)
        {
            Stored = snapshot;

            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class NoopPresenter : IScaffoldPresenter
    {
        public Task SynchronizeAsync(ScaffoldRoot root, ScaffoldPresentationHint hint) => Task.CompletedTask;

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
    }

    /// <summary>One "process": DI + engine + scaffold + restore service sharing the given store.</summary>
    private sealed class Harness : IDisposable
    {
        public ServiceProvider ServiceProvider { get; }
        public NavigationService NavigationService { get; }
        public Scaffold Scaffold { get; }
        public ScaffoldTabBar TabBar { get; }
        public NavigationRestoreService Restore { get; }
        public INavigationRestore RestoreApi => Restore;
        public TestTimeProvider TimeProvider { get; }

        /// <param name="store">The shared "disk" surviving across harnesses.</param>
        /// <param name="configureRestore">Extra restore options.</param>
        /// <param name="withRestore">False leaves restore unconfigured (inert service).</param>
        /// <param name="configureServices">
        /// Late service overrides (last registration wins) — e.g. replacing a page model
        /// substitute with one wired to call restore APIs from its lifecycle.
        /// </param>
        public Harness(
            InMemoryStore store,
            Action<NavigationRestoreOptions>? configureRestore = null,
            bool withRestore = true,
            Action<IServiceCollection>? configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
            services.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());

            var configurator = new NavigationConfigurator(services);
            var mapping = (IDictionary<Type, Type>) configurator.Mapping;
            mapping.Add(typeof(IHomePageModel), typeof(HomePage));
            mapping.Add(typeof(ISearchPageModel), typeof(SearchPage));
            mapping.Add(typeof(IDetailPageModel), typeof(DetailPage));
            mapping.Add(typeof(IDeepDetailPageModel), typeof(DeepDetailPage));
            configurator.AddPage<ViewOnlyPage>();

            if (withRestore)
            {
                // Mirrors UseNaluNavigationRestore: the options live in DI, not on the configurator.
                var options = new NavigationRestoreOptions();
                options.AddIntent<SearchIntent>();
                options.AddIntent<DetailIntent>();
                options.AddIntent<DeepDetailIntent>();
                options.AddIntent<HydratableIntent>();
                configureRestore?.Invoke(options);
                services.AddSingleton(options);
            }

            services.AddScoped(_ => Substitute.For<IHomePageModel>());
            services.AddScoped<HomePage>();
            services.AddScoped(_ => Substitute.For<ISearchPageModel>());
            services.AddScoped<SearchPage>();
            services.AddScoped(_ => Substitute.For<IDetailPageModel>());
            services.AddScoped<DetailPage>();
            services.AddScoped(_ => Substitute.For<IDeepDetailPageModel>());
            services.AddScoped<DeepDetailPage>();

            services.AddSingleton<INavigationService, NavigationService>();

            TimeProvider = new TestTimeProvider();
            services.AddSingleton<TimeProvider>(TimeProvider);
            services.AddSingleton<NavigationRestoreService>();
            services.AddSingleton<INavigationRestore>(sp => sp.GetRequiredService<NavigationRestoreService>());
            services.AddSingleton<IIntentSerializer>(sp => new NavigationDefaultIntentSerializer(sp.GetService<NavigationRestoreOptions>()));
            services.AddSingleton<INavigationRestoreStore>(store);

            configureServices?.Invoke(services);

            ServiceProvider = services.BuildServiceProvider();
            NavigationService = (NavigationService) ServiceProvider.GetRequiredService<INavigationService>();
            Restore = ServiceProvider.GetRequiredService<NavigationRestoreService>();

            TabBar = new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { PageType = typeof(HomePage) },
                    new ScaffoldRoot { PageType = typeof(SearchPage) }
                }
            };

            Scaffold = new Scaffold
            {
                Areas = { TabBar },
                Presenter = new NoopPresenter()
            };
        }

        public Task BootAsync() => Scaffold.InitializeAndPresentAsync(ServiceProvider);

        public ScaffoldRoot HomeRoot => TabBar.Roots[0];

        public ScaffoldRoot SearchRoot => TabBar.Roots[1];

        public IReadOnlyList<NavigationStackPage> HomeStack => HomeRoot.NavigationStack.PushedPages;

        public IReadOnlyList<NavigationStackPage> SearchStack => SearchRoot.NavigationStack.PushedPages;

        public void Dispose()
        {
            Scaffold.Dispose();
            ServiceProvider.Dispose();
        }
    }

    private static NavigationRestoreSnapshot ParseSnapshot(string payload)
        => JsonSerializer.Deserialize(payload, NavigationRestoreJsonContext.Default.NavigationRestoreSnapshot)!;

    private static string WriteSnapshot(NavigationRestoreSnapshot snapshot)
        => JsonSerializer.Serialize(snapshot, NavigationRestoreJsonContext.Default.NavigationRestoreSnapshot);

    [Fact(DisplayName = "Capture is automatic: pushed pages and their serializable intents enter the snapshot")]
    public async Task CaptureIsAutomatic()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("ctx")));
        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>());
        await harness.Restore.FlushAsync();

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.RootSegment.Should().Be("HomePage");
        snapshot.Frames.Should().HaveCount(2);
        snapshot.Frames[0].Segment.Should().Be("DetailPage");
        snapshot.Frames[0].Intent!.TypeId.Should().Be("DetailIntent");
        snapshot.Frames[0].Intent!.Payload.Should().Contain("ctx");

        // No intent ⇒ restorable without one.
        snapshot.Frames[1].Segment.Should().Be("DeepDetailPage");
        snapshot.Frames[1].Intent.Should().BeNull();
    }

    [Fact(DisplayName = "An unregistered intent ends the restorable prefix at that page")]
    public async Task UnregisteredIntentEndsThePrefix()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("ok")));
        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>().WithIntent(new OpaqueIntent("live-only")));
        await harness.Restore.FlushAsync();

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames.Should().ContainSingle().Which.Segment.Should().Be("DetailPage");
    }

    [Fact(DisplayName = "Capture prunes popped pages automatically")]
    public async Task CapturePrunesPoppedPages()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        await harness.Restore.FlushAsync();
        ParseSnapshot(store.Stored!).Frames.Should().ContainSingle();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Pop());
        await harness.Restore.FlushAsync();

        ParseSnapshot(store.Stored!).Frames.Should().BeEmpty();
    }

    [Fact(DisplayName = "ForgetAsync from a lifecycle callback (mid multi-push) removes the page via the ambient lifecycle page")]
    public async Task ForgetAsyncFromLifecycleRemovesThePage()
    {
        var store = new InMemoryStore();

        // The wizard page forgets itself from OnEnteringAsync — during a MULTI-PUSH the page
        // is NOT on the committed stack yet: only the AsyncLocal ambient lifecycle page can
        // resolve it (the stack-top fallback would target the wrong page).
        using var harness = new Harness(
            store,
            configureServices: services => services.AddScoped(sp =>
                {
                    var model = Substitute.For<IDeepDetailPageModel>();

                    ((IEnteringAware) model).OnEnteringAsync()
                                            .Returns(_ => new ValueTask(sp.GetRequiredService<INavigationRestore>().ForgetAsync()));

                    return model;
                }
            )
        );

        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(
            Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>()
        );

        await harness.Restore.FlushAsync();

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames.Should().ContainSingle().Which.Segment.Should().Be("DetailPage");
    }

    [Fact(DisplayName = "ForgetAsync outside a lifecycle callback targets the current top page")]
    public async Task ForgetAsyncOutsideLifecycleTargetsTheCurrentPage()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>());

        // Command-style call: no lifecycle running ⇒ the current top page is forgotten.
        await harness.RestoreApi.ForgetAsync();
        await harness.Restore.FlushAsync();

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames.Should().ContainSingle().Which.Segment.Should().Be("DetailPage");
    }

    [Fact(DisplayName = "ForgetAsync on the root produces a snapshot that restores nothing")]
    public async Task ForgetAsyncOnTheRootRestoresNothing()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.RestoreApi.ForgetAsync(); // current page = home root
        }

        using var second = new Harness(store);
        await second.BootAsync();

        second.TabBar.CurrentRoot.Should().Be(second.HomeRoot);
        second.HomeStack.Should().BeEmpty();
    }

    [Fact(DisplayName = "Killing the app on a forgotten page restores the pages below it")]
    public async Task KillingOnAForgottenPageRestoresThePagesBelowIt()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("kept")));
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>());

            // Forgetting excludes THIS page (and anything later pushed on top of it) — the
            // rest of the stack keeps restoring.
            await first.RestoreApi.ForgetAsync();
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().ContainSingle().Which.Page.Should().BeOfType<DetailPage>();
    }

    [Fact(DisplayName = "Popping a forgotten page resumes tracking for what follows")]
    public async Task PoppingAForgottenPageResumesTracking()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>());

            // Wizard-style: the top page forgets itself, then pops. The exclusion lives on
            // the page INSTANCE, so it dies with the pop — everything after tracks normally.
            await first.RestoreApi.ForgetAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Pop());
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().HaveCount(2);
        second.HomeStack[0].Page.Should().BeOfType<DetailPage>();
        second.HomeStack[1].Page.Should().BeOfType<DeepDetailPage>();
    }

    [Fact(DisplayName = "A forgotten root keeps its whole stack out of the snapshot, even when revisited")]
    public async Task ForgottenRootStaysUntrackedIncludingItsStack()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.RestoreApi.ForgetAsync(); // current page = home root

        // Pushes on the forgotten root are untracked too: their context builds on it.
        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
        await harness.Restore.FlushAsync();
        ParseSnapshot(store.Stored!).RootSegment.Should().BeNull("the forgotten root's stack builds on it");

        // Roots never pop: leaving and returning does not lift the exclusion.
        (await harness.NavigationService.GoToAsync(Navigation.Absolute().Root<ISearchPageModel>())).Should().BeTrue();
        (await harness.NavigationService.GoToAsync(Navigation.Absolute().Root<IHomePageModel>())).Should().BeTrue();
        await harness.Restore.FlushAsync();

        ParseSnapshot(store.Stored!).RootSegment.Should().BeNull("the root instance is still alive, so its exclusion persists");
    }

    [Fact(DisplayName = "Other roots keep tracking after a root forgot itself")]
    public async Task OtherRootsKeepTrackingAfterARootForgotItself()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.RestoreApi.ForgetAsync(); // home root forgets itself

            // The snapshot always describes the CURRENT root: switching to an unforgotten
            // root re-enables capture in full, stack and intents included.
            (await first.NavigationService.GoToAsync(Navigation.Absolute().Root<ISearchPageModel>())).Should().BeTrue();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("kept")));
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        await second.BootAsync();

        second.TabBar.CurrentRoot.Should().Be(second.SearchRoot);
        second.SearchStack.Should().ContainSingle().Which.Page.Should().BeOfType<DetailPage>();
    }

    [Fact(DisplayName = "RestoreWithIntentAsync replaces the captured intent for the current page")]
    public async Task RestoreWithIntentReplacesTheCapturedIntent()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("draft")));
        await harness.RestoreApi.RestoreWithIntentAsync(new DetailIntent("saved-42"));

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames.Should().ContainSingle().Which.Intent!.Payload.Should().Contain("saved-42");
    }

    [Fact(DisplayName = "RestoreWithIntentAsync makes an opaque-intent page restorable again")]
    public async Task RestoreWithIntentOverridesAnOpaqueIntent()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>().WithIntent(new OpaqueIntent("live-only")));
        await harness.Restore.FlushAsync();
        ParseSnapshot(store.Stored!).Frames.Should().BeEmpty();

        // The page opts back in with a serializable equivalent of its context.
        await harness.RestoreApi.RestoreWithIntentAsync(new DeepDetailIntent("serializable-equivalent"));

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames.Should().ContainSingle().Which.Intent!.TypeId.Should().Be("DeepDetailIntent");
    }

    [Fact(DisplayName = "Restore round-trip lands on the captured stack and replays intents (chunked)")]
    public async Task RestoreRoundTripLandsOnCapturedState()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("detail-context")));
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>().WithIntent(new DeepDetailIntent("deep-context")));
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().HaveCount(2);
        second.HomeStack[0].Page.Should().BeOfType<DetailPage>();
        second.HomeStack[1].Page.Should().BeOfType<DeepDetailPage>();

        var detailModel = (IDetailPageModel) second.HomeStack[0].Page.BindingContext;
        await detailModel.Received(1).OnEnteringAsync(new DetailIntent("detail-context"));
        var deepModel = (IDeepDetailPageModel) second.HomeStack[1].Page.BindingContext;
        await deepModel.Received(1).OnEnteringAsync(new DeepDetailIntent("deep-context"));

        // The snapshot was deleted at boot and re-persisted after the replay: intents were
        // re-recorded by the replay navigations themselves (capture is automatic).
        store.Stored.Should().NotBeNull();
        ParseSnapshot(store.Stored!).Frames.Should().HaveCount(2);
    }

    [Fact(DisplayName = "JsonIgnore intent state is rehydrated via IIntentHydrator before the replay navigation")]
    public async Task JsonIgnoreIntentStateIsRehydratedBeforeReplay()
    {
        var store = new InMemoryStore();
        var heavyObject = new object();

        using (var first = new Harness(store))
        {
            await first.BootAsync();

            await first.NavigationService.GoToAsync(
                Navigation.Relative().Push<IDetailPageModel>().WithIntent(new HydratableIntent { Value = "kept", Heavy = heavyObject })
            );

            await first.Restore.FlushAsync();
        }

        // The [JsonIgnore] property never reached the snapshot.
        store.Stored.Should().Contain("kept").And.NotContain("Heavy");

        // The HOME root model (below the restored page — the engine walks the restoring stack
        // top→root) hydrates the intent before the replay navigates with it.
        var rehydrated = new object();

        using var second = new Harness(
            store,
            configureServices: services => services.AddScoped(_ =>
                {
                    var model = Substitute.For<IHomePageModel>();

                    model.HydrateAsync(Arg.Any<HydratableIntent>())
                         .Returns(call =>
                             {
                                 call.Arg<HydratableIntent>().Heavy = rehydrated;

                                 return ValueTask.CompletedTask;
                             }
                         );

                    return model;
                }
            )
        );

        await second.BootAsync();

        var detailModel = (IDetailPageModel) second.HomeStack[0].Page.BindingContext;
        await detailModel.Received(1).OnEnteringAsync(Arg.Is<HydratableIntent>(intent => intent.Value == "kept" && intent.Heavy == rehydrated));
    }

    [Fact(DisplayName = "Restore replays AFTER the configured initial root ran (initialization flows execute first)")]
    public async Task RestoreReplaysAfterTheInitialRootRan()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Absolute().Root<ISearchPageModel>().WithIntent(new SearchIntent("root-context")));
            await first.Restore.FlushAsync();
            ParseSnapshot(store.Stored!).RootSegment.Should().Be("SearchPage");
        }

        using var second = new Harness(store);
        await second.BootAsync();

        // The configured initial root (Home) was created and ran its lifecycle FIRST…
        second.HomeRoot.NavigationStack.RootPage.Should().NotBeNull("the initialization root always runs before the replay");
        var homeModel = (IHomePageModel) second.HomeRoot.NavigationStack.RootPage!.BindingContext;
        await homeModel.Received(1).OnAppearingAsync();

        // …then the replay landed on the captured root, delivering its captured intent.
        second.TabBar.CurrentRoot.Should().Be(second.SearchRoot);
        var searchModel = (ISearchPageModel) second.SearchRoot.NavigationStack.RootPage!.BindingContext;
        await searchModel.Received(1).OnEnteringAsync(new SearchIntent("root-context"));
    }

    [Fact(DisplayName = "While a restore is pending, app navigations are ignored; afterwards they work again")]
    public async Task PendingRestoreIgnoresAppNavigations()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        // The initialization root's appearing attempts a redirect (the classic init-flow
        // pattern): with a restore pending, that navigation must be IGNORED — deterministic
        // replay — instead of racing it.
        bool? redirectResult = null;

        using var second = new Harness(
            store,
            configureServices: services => services.AddScoped(sp =>
                {
                    var model = Substitute.For<IHomePageModel>();

                    // Redirect on the FIRST appearing only (the init-flow pattern) — home
                    // re-appears later on pops, where a navigation would legally throw the
                    // engine's within-a-navigation guard.
                    model.OnAppearingAsync()
                         .Returns(
                             _ => new ValueTask(RedirectAsync(sp.GetRequiredService<INavigationService>())),
                             _ => ValueTask.CompletedTask
                         );

                    return model;

                    async Task RedirectAsync(INavigationService navigation)
                        => redirectResult = await navigation.GoToAsync(Navigation.Absolute().Root<ISearchPageModel>());
                }
            )
        );

        await second.BootAsync();

        redirectResult.Should().BeFalse("navigations not issued by the replay are ignored while a restore is pending");
        second.HomeStack.Should().ContainSingle().Which.Page.Should().BeOfType<DetailPage>();

        // After the replay, navigations work again.
        (await second.NavigationService.GoToAsync(Navigation.Relative().Pop())).Should().BeTrue();
    }

    [Fact(DisplayName = "TryStopRestoreAsync discards the pending restore and lifts the suppression window")]
    public async Task TryStopRestoreDiscardsThePendingRestore()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);

        // Stop the restore the moment the initialization root enters (an auth-style flow).
        var stopped = false;
        NavigationRestoreService restore = second.Restore;

        second.Scaffold.NavigationEvent += (_, e) =>
        {
            if (e is { EventType: NavigationLifecycleEventType.Entering, Target: IHomePageModel } && !stopped)
            {
                stopped = true;
                _ = restore.TryStopRestoreAsync();
            }
        };

        await second.BootAsync();

        // No replay happened; the app is free to navigate.
        second.HomeStack.Should().BeEmpty();
        (await second.NavigationService.GoToAsync(Navigation.Absolute().Root<ISearchPageModel>())).Should().BeTrue();
        second.TabBar.CurrentRoot.Should().Be(second.SearchRoot);
    }

    [Fact(DisplayName = "Suppression stays armed through intermediate replay steps and lifts before the final one")]
    public async Task SuppressionLiftsBeforeTheFinalReplayNavigation()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();

            // Two chunks: [Detail(intent)] then [DeepDetail] — Detail is an INTERMEDIATE
            // replay step, DeepDetail is the FINAL restored destination.
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("ctx")));
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        bool? suppressedDuringIntermediate = null;
        bool? suppressedDuringFinal = null;

        second.Scaffold.NavigationEvent += (_, e) =>
        {
            if (e.EventType != NavigationLifecycleEventType.Entering)
            {
                return;
            }

            switch (e.Target)
            {
                case IDetailPageModel:
                    suppressedDuringIntermediate = second.Restore.IsSuppressionActive;

                    break;

                case IDeepDetailPageModel:
                    suppressedDuringFinal = second.Restore.IsSuppressionActive;

                    break;
            }
        };

        await second.BootAsync();

        second.HomeStack.Should().HaveCount(2);
        suppressedDuringIntermediate.Should().BeTrue("intermediate restored pages' dispatched auto-navigations must drain inside the window");
        suppressedDuringFinal.Should().BeFalse("the LAST restored destination keeps its right to auto-navigate");
    }

    [Fact(DisplayName = "TryStopRestoreAsync returns false when nothing is pending")]
    public async Task TryStopRestoreReturnsFalseWhenNothingIsPending()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        (await harness.RestoreApi.TryStopRestoreAsync()).Should().BeFalse();
    }

    [Fact(DisplayName = "Restore round-trips view-only pages (no page model, not in Mapping)")]
    public async Task RestoreRoundTripsViewOnlyPages()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<ViewOnlyPage>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().ContainSingle().Which.Page.Should().BeOfType<ViewOnlyPage>();
    }

    [Fact(DisplayName = "Restore runs once per launch: a second host in the same process boots normally")]
    public async Task RestoreRunsOncePerLaunch()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        await second.BootAsync();
        second.HomeStack.Should().ContainSingle("the first host in the process restores");

        // A logout/login-style swap: new scaffold, same service provider (same process).
        var snapshotAfterRestore = store.Stored;
        second.Scaffold.Dispose();

        var replacement = new Scaffold
        {
            Areas = { new ScaffoldTabBar { Roots = { new ScaffoldRoot { PageType = typeof(HomePage) }, new ScaffoldRoot { PageType = typeof(SearchPage) } } } },
            Presenter = new NoopPresenter()
        };

        try
        {
            await replacement.InitializeAndPresentAsync(second.ServiceProvider);

            replacement.CurrentArea!.CurrentRoot!.NavigationStack.PushedPages.Should().BeEmpty("restore replays only at app launch");
            snapshotAfterRestore.Should().NotBeNull();
        }
        finally
        {
            replacement.Dispose();
        }
    }

    [Fact(DisplayName = "Snapshot with a different app version is discarded")]
    public async Task SnapshotWithDifferentAppVersionIsDiscarded()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            first.Restore.AppVersionProvider = () => "1.0.0+1";
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store);
        second.Restore.AppVersionProvider = () => "1.1.0+2";
        await second.BootAsync();

        second.HomeStack.Should().BeEmpty();
    }

    [Fact(DisplayName = "Snapshot older than MaxAge is discarded")]
    public async Task SnapshotOlderThanMaxAgeIsDiscarded()
    {
        var store = new InMemoryStore();
        Action<NavigationRestoreOptions> configure = options => options.MaxAge = TimeSpan.FromHours(1);

        using (var first = new Harness(store, configure))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        using var second = new Harness(store, configure);
        second.TimeProvider.Now = second.TimeProvider.Now.AddHours(2);
        await second.BootAsync();

        second.HomeStack.Should().BeEmpty();
    }

    [Fact(DisplayName = "Snapshot with a mismatching route hash is discarded")]
    public async Task SnapshotWithMismatchingRouteHashIsDiscarded()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        // Tampered/stale hash (e.g. the app's root structure changed): the snapshot is discarded.
        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.RouteHash = "0000000000000000";
        store.Stored = WriteSnapshot(snapshot);

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().BeEmpty();
    }

    [Fact(DisplayName = "Snapshot frame with an unknown segment truncates the restored prefix")]
    public async Task SnapshotFrameWithUnknownSegmentTruncatesThePrefix()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames[1].Segment = "RemovedPage";
        store.Stored = WriteSnapshot(snapshot);

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().ContainSingle().Which.Page.Should().BeOfType<DetailPage>();
    }

    [Fact(DisplayName = "Corrupted snapshot payload boots the default destination")]
    public async Task CorruptedSnapshotBootsTheDefaultDestination()
    {
        var store = new InMemoryStore { Stored = "{ not json" };
        using var harness = new Harness(store);

        await harness.BootAsync();

        harness.TabBar.CurrentRoot.Should().Be(harness.HomeRoot);
        harness.HomeRoot.NavigationStack.RootPage.Should().BeOfType<HomePage>();
    }

    [Fact(DisplayName = "Restore service is inert when UseNaluNavigationRestore was not called")]
    public async Task RestoreServiceIsInertWhenNotEnabled()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store, withRestore: false);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("ignored")));

        // Per-page methods no-op without throwing — shared pages call them unconditionally.
        await harness.RestoreApi.ForgetAsync();
        (await harness.RestoreApi.TryStopRestoreAsync()).Should().BeFalse();

        await harness.Restore.FlushAsync();
        store.Stored.Should().BeNull();
    }

    [Fact(DisplayName = "RestoreWithIntentAsync with an unregistered intent type throws at the call site")]
    public async Task RestoreWithUnregisteredIntentThrows()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        var act = () => harness.RestoreApi.RestoreWithIntentAsync(new OpaqueIntent("nope"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not registered*");
    }

    [Fact(DisplayName = "Intent type id collisions are rejected at registration")]
    public void IntentTypeIdCollisionsAreRejected()
    {
        var options = new NavigationRestoreOptions();
        options.AddIntent<DetailIntent>("same-id");

        var act = () => options.AddIntent<DeepDetailIntent>("same-id");

        act.Should().Throw<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Fact(DisplayName = "Snapshot frame whose intent type no longer resolves truncates the restored prefix")]
    public async Task SnapshotFrameWithUnresolvableIntentTypeTruncatesThePrefix()
    {
        var store = new InMemoryStore();

        using (var first = new Harness(store))
        {
            await first.BootAsync();
            await first.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().Push<IDeepDetailPageModel>());
            await first.Restore.FlushAsync();
        }

        // The second frame claims an intent type that does not exist anymore (renamed/removed).
        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames[1].Intent = new NavigationRestoreIntentData { TypeId = "No.Such.IntentType", Payload = "{}" };
        store.Stored = WriteSnapshot(snapshot);

        using var second = new Harness(store);
        await second.BootAsync();

        second.HomeStack.Should().ContainSingle().Which.Page.Should().BeOfType<DetailPage>();
    }

    [Fact(DisplayName = "Pop intents never replace the revealed page's captured entering intent")]
    public async Task PopIntentsDoNotReplaceEnteringIntents()
    {
        var store = new InMemoryStore();
        using var harness = new Harness(store);
        await harness.BootAsync();

        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDetailPageModel>().WithIntent(new DetailIntent("entering-context")));
        await harness.NavigationService.GoToAsync(Navigation.Relative().Push<IDeepDetailPageModel>());

        // Pop delivering a RESULT intent to Detail's appearing: appearing context, not
        // entering context — Detail must still restore with its original entering intent.
        await harness.NavigationService.GoToAsync(Navigation.Relative().Pop().WithIntent(new DetailIntent("pop-result")));
        await harness.Restore.FlushAsync();

        var snapshot = ParseSnapshot(store.Stored!);
        snapshot.Frames.Should().ContainSingle().Which.Intent!.Payload.Should().Contain("entering-context");
    }
}
