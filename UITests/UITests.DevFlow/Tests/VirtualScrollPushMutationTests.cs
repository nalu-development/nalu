using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Regression tests for crashes observed when the ObservableCollection bound to a
/// VirtualScroll is mutated WHILE the page hosting it is being pushed (or popped):
/// change sets reach the platform view during the navigation animation, before/while
/// the handler attaches or detaches.
/// </summary>
/// <remarks>
/// The TestApp page starts a UI-thread timer that fires 15 mutations at 40ms intervals,
/// beginning together with the (animated) push. When the run survives, the pushed page's
/// MutationStatusLabel reports "Done {finalCount}". A crash keeps the label at "Running"
/// (and kills the DevFlow agent), failing the test.
/// </remarks>
public class VirtualScrollPushMutationTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll Push Mutation Tests";
    private static readonly TimeSpan _mutationTimeout = TimeSpan.FromSeconds(10);

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("PushAddButton");
    }

    [Theory]
    // 20 items + 15 appended during the push animation (the new tail stays virtualized out).
    [InlineData("PushAddButton", "Done 35", "P1")]
    // 15 inserted at position 0: the most recent one ends up on top.
    [InlineData("PushInsertTopButton", "Done 35", "P35")]
    // 15 removed from the top: P16..P20 remain.
    [InlineData("PushRemoveButton", "Done 5", "P16")]
    // 5 adds, a Clear at tick 5, then 9 more adds (P26..P34).
    [InlineData("PushClearButton", "Done 9", "P26")]
    // Rotating add/insert/remove/move/replace: 20 + 3 adds + 3 inserts - 3 removes.
    [InlineData("PushMixedButton", "Done 23", "PushHeader")]
    public async Task MutatingWhilePushingDoesNotCrash(string scenarioButton, string expectedStatus, string expectedElement)
    {
        await OpenPageAsync();

        await App.TapAsync(scenarioButton);

        // The mutation loop outlives the push animation; "Done N" proves both completed.
        await App.WaitForTextAsync("MutationStatusLabel", expectedStatus, _mutationTimeout);

        (await App.WaitForElementAsync(expectedElement)).IsVisible.Should().BeTrue();

        // The list is still functional after the mutation storm: the header is at the top.
        (await App.WaitForElementAsync("PushHeader")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task MutatingWhilePoppingDoesNotCrash()
    {
        await OpenPageAsync();

        // Pushes, then pops as soon as the push completes; mutations keep firing
        // through the pop animation and after the page is gone.
        await App.TapAsync("PushPopButton");

        await App.WaitForTextAsync("RootStatusLabel", "Popped", _mutationTimeout);
        await App.WaitForTextAsync("MutationDoneLabel", "Done 23", _mutationTimeout);

        // Back on the root page with the app still responsive.
        (await App.WaitForElementAsync("PushAddButton")).IsVisible.Should().BeTrue();
    }
}
