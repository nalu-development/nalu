using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Item drag&amp;drop against the "Virtual Scroll Drag Tests" harness.
/// Android drives a REAL long-press drag host-side (<see cref="NaluApp.AndroidLongPressDragAsync"/>,
/// discrete adb motion events — ItemTouchHelper needs a held long-press synthetic agent
/// gestures cannot produce). Apple platforms cannot receive injected held long-presses from
/// the test host at all, so the harness page exposes buttons driving the library's internal
/// drag simulator — the same interactive-movement sequence the gesture handler performs,
/// bypassing only Apple's long-press recognition.
/// </summary>
public class VirtualScrollDragTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Virtual Scroll Drag Tests";

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("DragCellA");
        await App.WaitForTextAsync("DragOrderLabel", "A,B,PIN,C,D,E,F,G");
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>Real gesture on Android; simulator button on Apple platforms.</summary>
    private async Task DragAsync(string fromCellId, string toCellId, string simButtonId)
    {
        if (await IsAndroidAsync())
        {
            await App.AndroidLongPressDragAsync(fromCellId, toCellId);
        }
        else
        {
            await App.TapAsync(simButtonId);
        }
    }

    [Fact]
    public async Task LongPressDragReordersItemAndRaisesLifecycle()
    {
        // Drag A (row 0) down over three rows and drop where D sits.
        await DragAsync("DragCellA", "DragCellD", "SimDragAD");

        // The collection order is the ground truth: A must have left the head and landed
        // between C and E (the exact slot may differ by one depending on where the final
        // midpoint crossing happened — both are valid outcomes of the platform reorder).
        var order = await App.WaitForTextMatchAsync("DragOrderLabel", text => text is not null && !text.StartsWith("A,", StringComparison.Ordinal));
        Assert.True(order is "B,PIN,C,A,D,E,F,G" or "B,PIN,C,D,A,E,F,G", $"Unexpected order after drag: {order}");

        // Exactly one drag lifecycle, with at least one committed move.
        await App.WaitForTextMatchAsync("DragStatusLabel", text => text is not null && text.StartsWith("S:1 E:1 M:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VetoedItemDoesNotDrag()
    {
        // PIN is rejected by CanDragItem: the same gesture must produce no reorder and no
        // drag lifecycle at all.
        await DragAsync("DragCellPIN", "DragCellE", "SimDragPIN");

        // Deterministic settle point: run a REAL drag afterwards and observe its lifecycle —
        // if the vetoed gesture had produced anything, the final order or the counts would
        // differ from a single B-drag alone.
        await DragAsync("DragCellB", "DragCellD", "SimDragBD");

        var order = await App.WaitForTextMatchAsync("DragOrderLabel", text => text is not null && !text.StartsWith("A,B,", StringComparison.Ordinal));
        Assert.True(order is "A,PIN,C,B,D,E,F,G" or "A,PIN,C,D,B,E,F,G", $"Unexpected order after vetoed+real drag: {order}");

        await App.WaitForTextMatchAsync("DragStatusLabel", text => text is not null && text.StartsWith("S:1 E:1 M:", StringComparison.Ordinal));
    }
}
