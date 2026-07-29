using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the model-first overlay service (IOverlayService + AddOverlay) against the
/// "Scaffold Overlay Service Tests" harness: intent delivery via the reflection-dispatched
/// OnEnteringAsync, result flow through IOverlayRef (typed, runtime-checked), dismissal
/// yielding default, the attached-presentation channel (MaxWidth on the view), and the
/// per-presentation lifecycle (entering → leaving → dispose with the DI scope).
/// </summary>
public class ScaffoldOverlayServiceChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Overlay Service Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task SheetModelReceivesIntentAndClosesWithResult()
    {
        await WaitDisplayedAsync("OverlayServiceHomePage");

        await App.TapAsync("ShowVmSheetButton");

        // Intent delivered through the reflection-dispatched OnEnteringAsync.
        await App.WaitForTextAsync("VmSheetText", "hello-intent");

        // The view's ATTACHED MaxWidth (300) caps the sheet surface, centered.
        var sheet = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");
        var scrim = await App.WaitForStableBoundsAsync("SheetScrim");
        sheet.Width.Should().BeApproximately(300, 4, "the view declares ScaffoldBottomSheet.MaxWidth = 300");
        (sheet.X + sheet.Width / 2).Should().BeApproximately(scrim.X + scrim.Width / 2, 8, "a capped sheet floats centered");

        await App.TapAsync("VmSheetCloseResultButton");
        await App.WaitForTextAsync("OverlayResultLabel", "result:42");

        // Full model lifecycle: entering (with intent), leaving on close, dispose with the scope.
        await App.WaitForTextAsync("OverlayLifecycleLabel", "lifecycle:entered,left,disposed");
    }

    [Fact]
    public async Task SheetDismissalYieldsDefaultResult()
    {
        await WaitDisplayedAsync("OverlayServiceHomePage");

        await App.TapAsync("ShowVmSheetButton");
        await App.WaitForTextAsync("VmSheetText", "hello-intent");

        await App.TapAsync("SheetScrim");
        await App.WaitForTextAsync("OverlayResultLabel", "result:0");
        await App.WaitForTextAsync("OverlayLifecycleLabel", "lifecycle:entered,left,disposed");
    }

    [Fact]
    public async Task WrongResultTypeThrowsAndKeepsSheetOpen()
    {
        await WaitDisplayedAsync("OverlayServiceHomePage");

        await App.TapAsync("ShowVmSheetButton");
        await App.WaitForTextAsync("VmSheetText", "hello-intent");

        // The runtime check throws BEFORE the close: the sheet stays open.
        await App.TapAsync("VmSheetCloseWrongTypeButton");
        await App.WaitForTextAsync("OverlayLifecycleLabel", "lifecycle:entered,wrong-type-threw");
        (await App.WaitForElementAsync("VmSheetText")).Should().NotBeNull();

        await App.TapAsync("VmSheetCloseResultButton");
        await App.WaitForTextAsync("OverlayResultLabel", "result:42");
    }

    [Fact]
    public async Task PopupModelResolvesThroughViewCtorAndReturnsResult()
    {
        await WaitDisplayedAsync("OverlayServiceHomePage");

        await App.TapAsync("ShowVmPopupButton");
        await WaitDisplayedAsync("VmPopupContent");

        await App.TapAsync("VmPopupPickButton");
        await App.WaitForTextAsync("OverlayResultLabel", "presult:picked");
    }
}
