namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// Pins the unsupported-platform contract: this test assembly targets plain net10.0 — the
/// exact assembly Windows/Mac Catalyst apps get via NuGet TFM fallback. UseNaluScaffold must
/// be callable, register <see cref="IOverlayService"/> and <see cref="IScaffoldFlyoutController"/>,
/// and every call must no-op gracefully (default results, no UI, no throw) while the app is
/// not scaffold-hosted.
/// </summary>
public class ScaffoldUnsupportedPlatformTests
{
    // ReSharper disable once NotAccessedPositionalProperty.Local
    private sealed record SomeOverlayModel(IOverlayRef Overlay);

    private static IServiceProvider BuildProvider()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);

        builder.UseNaluScaffold(scaffold => scaffold.AddOverlay<SomeOverlayModel, ContentView>(static (_, _) => new ContentView()));
        // As in the real app: the navigation service is a singleton.
        builder.Services.AddSingleton(static _ => Substitute.For<INavigationService>());

        return builder.Build().Services;
    }

    private static IServiceScope BuildScope() => BuildProvider().CreateScope();

    [Fact(DisplayName = "IOverlayService is a singleton: resolvable from the root and shared across scopes")]
    public void OverlayServiceIsASingleton()
    {
        var provider = BuildProvider();

        var fromRoot = provider.GetRequiredService<IOverlayService>();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        scopeA.ServiceProvider.GetRequiredService<IOverlayService>().Should().BeSameAs(fromRoot);
        scopeB.ServiceProvider.GetRequiredService<IOverlayService>().Should().BeSameAs(fromRoot);
    }

    [Fact(DisplayName = "UseNaluScaffold registers both services on the neutral TFM")]
    public void ServicesAreRegistered()
    {
        using var scope = BuildScope();

        scope.ServiceProvider.GetService<IOverlayService>().Should().NotBeNull();
        scope.ServiceProvider.GetService<IScaffoldFlyoutController>().Should().NotBeNull();
    }

    [Fact(DisplayName = "Overlay service calls no-op with default results while not scaffold-hosted")]
    public async Task OverlayServiceNoOps()
    {
        using var scope = BuildScope();
        var overlays = scope.ServiceProvider.GetRequiredService<IOverlayService>();

        await overlays.ShowPopupAsync<SomeOverlayModel>();
        await overlays.ShowBottomSheetAsync<SomeOverlayModel>();
        (await overlays.ShowPopupAsync<SomeOverlayModel, bool>()).Should().BeFalse();
        (await overlays.ShowBottomSheetAsync<SomeOverlayModel, string>()).Should().BeNull();

        // Even unregistered models must not throw: the ambient-scaffold check comes first.
        await overlays.Invoking(static o => o.ShowPopupAsync<ScaffoldUnsupportedPlatformTests>()).Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Flyout controller calls no-op while not scaffold-hosted")]
    public async Task FlyoutControllerNoOps()
    {
        using var scope = BuildScope();
        var flyout = scope.ServiceProvider.GetRequiredService<IScaffoldFlyoutController>();

        await flyout.OpenAsync(ScaffoldFlyoutSide.Start);
        await flyout.OpenAsync(ScaffoldFlyoutSide.End);
        await flyout.CloseAsync();
    }
}
