using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Regression tests for the iOS "_Bug_Detected_In_Client_Of_UICollectionView_Invalid_Batch_Updates"
/// assertion: a burst of individually dispatched Insert(0) calls reaching the platform notifier
/// right after the platform view is created, BEFORE UICollectionView has performed its initial
/// data-loading layout (originally hit by lost-message results syncing line-by-line while the
/// Background Http Lifecycle page appeared post-relaunch).
/// </summary>
/// <remarks>
/// The TestApp page auto-fires the burst the moment the VirtualScroll handler connects — the
/// tightest possible race with the initial layout. A crash kills the DevFlow agent, so the
/// status label never reaching "Done" fails the test.
/// </remarks>
public class VirtualScrollBurstInsertTests(NaluApp app) : BaseUiTest(app)
{
    private const string _pageName = "Virtual Scroll Burst Insert Tests";
    private static readonly TimeSpan _burstTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task BurstInsertsAtPageAppearanceDoNotCrash()
    {
        // Opening the page triggers the burst automatically at handler-connect time.
        await App.OpenTestPageAsync(_pageName);

        await App.WaitForTextAsync("BurstStatusLabel", "Done: 10", _burstTimeout);

        // Insert(0) semantics: the last inserted item is on top and materialized.
        (await App.WaitForElementAsync("B10")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task BurstInsertsAgainstFreshCollectionViewDoNotCrash()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForTextAsync("BurstStatusLabel", "Done: 10", _burstTimeout);

        // Recreate swaps in a brand-new VirtualScroll (fresh, never-laid-out platform
        // collection view) and bursts during its handler connection — the same pre-initial-
        // layout window, re-entered deterministically without page navigation.
        await App.TapAsync("RecreateButton");
        await App.WaitForTextAsync("BurstStatusLabel", "Done: 10", _burstTimeout);
        (await App.WaitForElementAsync("B20")).IsVisible.Should().BeTrue();

        // A burst on the now-loaded collection view exercises the incremental path too.
        await App.TapAsync("BurstButton");
        await App.WaitForTextAsync("BurstStatusLabel", "Done: 20", _burstTimeout);
        (await App.WaitForElementAsync("B30")).IsVisible.Should().BeTrue();
    }
}
