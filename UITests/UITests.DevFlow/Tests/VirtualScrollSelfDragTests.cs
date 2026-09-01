using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Verifies the auto-wrapped drag&amp;drop pattern against the "Virtual Scroll Self Drag Tests"
/// harness — the FIRST XAML (source-generated) test page: a plain ObservableCollection bound to
/// ItemsSource with <c>DragHandler="{Binding Adapter, Source={RelativeSource Self}}"</c>.
/// Proves the compiled-XAML binding resolves to the coerced adapter (declared BEFORE ItemsSource,
/// so only the Adapter change notification can satisfy it), that a real drag reorders the plain
/// collection through it, and that the binding follows adapter recreation when the collection
/// instance is replaced. Android drives a real long-press drag; Apple platforms use the page's
/// internal drag-simulator button (same rationale as VirtualScrollDragTests).
/// </summary>
public class VirtualScrollSelfDragTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Virtual Scroll Self Drag Tests";
    private const string _freshOrder = "A,B,C,D,E,F,G,H";
    private const string _replacedOrder = "I,J,K,L,M,N,O,P";
    private const string _healthyStatus = "adapter:ok reorder:True same:True";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForElementAsync("SelfCellA");
        await App.WaitForTextAsync("SelfDragOrderLabel", _freshOrder);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>Real gesture on Android; simulator button (index 0 → 4) on Apple platforms.</summary>
    private async Task DragHeadPastThirdAsync(string fromCellId, string toCellId)
    {
        if ((await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase))
        {
            await App.AndroidLongPressDragAsync(fromCellId, toCellId);
        }
        else
        {
            await App.TapAsync("SimSelfDragAD");
        }
    }

    private async Task<string?> WaitForReorderAsync(string headItemName)
        => await App.WaitForTextMatchAsync("SelfDragOrderLabel", text => text is not null && !text.StartsWith($"{headItemName},", StringComparison.Ordinal));

    [Fact]
    public async Task SelfBoundDragHandlerResolvesToCoercedAdapter()
    {
        // The page has NO adapter of its own: DragHandler can only be non-null if the compiled
        // Self-binding picked up the adapter coerced from the plain ObservableCollection.
        await App.TapAsync("SelfDragVerifyButton");
        await App.WaitForTextAsync("SelfDragStatusLabel", _healthyStatus);
    }

    [Fact]
    public async Task DragReordersAutoWrappedCollectionThroughSelfBoundHandler()
    {
        await DragHeadPastThirdAsync("SelfCellA", "SelfCellD");

        // The collection order is the ground truth; the exact landing slot is deliberately not
        // pinned (see VirtualScrollDragTests for the rationale).
        var order = await WaitForReorderAsync("A");
        Assert.True(order?.StartsWith("B,C,", StringComparison.Ordinal) is true, $"A did not leave the head: {order}");
        Assert.True(order?.Split(',').Length == 8, $"Items were lost or duplicated by the drag: {order}");
    }

    [Fact]
    public async Task SelfBoundDragHandlerFollowsCollectionReplacement()
    {
        // Dirty the first collection so the replaced order proves the NEW collection is shown.
        await DragHeadPastThirdAsync("SelfCellA", "SelfCellD");
        await WaitForReorderAsync("A");

        await App.TapAsync("SelfDragReplaceButton");
        await App.WaitForTextAsync("SelfDragOrderLabel", _replacedOrder);

        // The re-coerced adapter must be the CURRENT drag handler...
        await App.TapAsync("SelfDragVerifyButton");
        await App.WaitForTextAsync("SelfDragStatusLabel", _healthyStatus);

        // ...and a drag must mutate the NEW collection: with a stale handler the old collection
        // would be reordered instead and this label would never change.
        await DragHeadPastThirdAsync("SelfCellI", "SelfCellL");
        var order = await WaitForReorderAsync("I");
        Assert.True(order?.Split(',').Length == 8, $"Items were lost or duplicated by the drag: {order}");
    }
}
