using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Gesture-driven item drag&amp;drop against the "Virtual Scroll Drag Tests" harness.
/// Android-only: ItemTouchHelper drag needs a REAL held long-press followed by a move —
/// synthetic agent gestures have no touch physics, so the drag is injected host-side via
/// <see cref="NaluApp.AndroidLongPressDragAsync"/> (discrete adb motion events).
/// </summary>
public class VirtualScrollDragTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Virtual Scroll Drag Tests";

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Android-only: real long-press drags are injected via adb motion events.");

        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("DragCellA");
        await App.WaitForTextAsync("DragOrderLabel", "A,B,PIN,C,D,E,F,G");
    }

    public async ValueTask DisposeAsync()
    {
        if (await IsAndroidAsync())
        {
            await App.ResetAsync();
        }
    }

    [Fact]
    public async Task LongPressDragReordersItemAndRaisesLifecycle()
    {
        // Drag A (row 0) down over three rows and drop where D sits.
        await App.AndroidLongPressDragAsync("DragCellA", "DragCellD");

        // The collection order is the ground truth: A must have left the head and landed
        // between C and E (the exact slot may differ by one depending on where the final
        // midpoint crossing happened — both are valid ItemTouchHelper outcomes).
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
        await App.AndroidLongPressDragAsync("DragCellPIN", "DragCellE");

        // Deterministic settle point: run a REAL drag afterwards and observe its lifecycle —
        // if the vetoed gesture had produced anything, the final order or the counts would
        // differ from a single B-drag alone.
        await App.AndroidLongPressDragAsync("DragCellB", "DragCellD");

        var order = await App.WaitForTextMatchAsync("DragOrderLabel", text => text is not null && !text.StartsWith("A,B,", StringComparison.Ordinal));
        Assert.True(order is "A,PIN,C,B,D,E,F,G" or "A,PIN,C,D,B,E,F,G", $"Unexpected order after vetoed+real drag: {order}");

        await App.WaitForTextMatchAsync("DragStatusLabel", text => text is not null && text.StartsWith("S:1 E:1 M:", StringComparison.Ordinal));
    }
}
