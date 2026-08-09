using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

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
    private sealed record SomeOverlayModel(IOverlayRef Overlay);

    private static IServiceScope BuildScope()
    {
        var builder = MauiApp.CreateBuilder(useDefaults: false);

        builder.UseNaluScaffold(scaffold => scaffold.AddOverlay<SomeOverlayModel, ContentView>(static (_, _) => new ContentView()));
        builder.Services.AddScoped(static _ => Substitute.For<INavigationService>());

        return builder.Build().Services.CreateScope();
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
