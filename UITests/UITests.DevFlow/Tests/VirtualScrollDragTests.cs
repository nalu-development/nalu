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

        // The collection order is the ground truth: A must have left the head and landed past C.
        // The exact slot is deliberately not pinned — a drop lands wherever the last midpoint
        // crossing put it, and the platforms (and the same platform under different load) settle
        // one row apart. Pinning it makes the test a stopwatch, not a statement about dragging.
        var order = await App.WaitForTextMatchAsync("DragOrderLabel", text => text is not null && !text.StartsWith("A,", StringComparison.Ordinal));
        Assert.True(order?.StartsWith("B,PIN,C,", StringComparison.Ordinal) is true, $"A did not land past C: {order}");
        Assert.True(order?.Split(',').Length == 8, $"Items were lost or duplicated by the drag: {order}");

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

        // What this test is about: PIN never moved (it is still second, right after A) and A is
        // still first — so the vetoed gesture reordered nothing. B moved past C, which is the
        // proof that the REAL drag afterwards did run; where exactly B landed is not pinned, for
        // the same reason as above.
        Assert.True(order?.StartsWith("A,PIN,C,", StringComparison.Ordinal) is true, $"The vetoed item moved, or B did not: {order}");
        Assert.True(order?.Split(',').Length == 8, $"Items were lost or duplicated by the drag: {order}");

        await App.WaitForTextMatchAsync("DragStatusLabel", text => text is not null && text.StartsWith("S:1 E:1 M:", StringComparison.Ordinal));
    }
}
