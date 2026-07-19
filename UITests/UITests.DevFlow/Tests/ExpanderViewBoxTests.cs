using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>ExpanderViewBox</c>: collapse/expand animation, <c>CollapsedHeight</c>
/// and the read-only <c>CanCollapse</c> property (including runtime content growth).
/// </summary>
public class ExpanderViewBoxTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Expander Tests";

    [Fact]
    public async Task CollapsedContentIsCappedAtCollapsedHeight()
    {
        await App.OpenTestPageAsync(PageName);

        // Content is 300 tall but CollapsedHeight is 100.
        var bounds = await App.WaitForStableBoundsAsync("ExpanderBox");
        bounds.Height.Should().BeApproximately(100, 1.5);

        (await App.WaitForElementAsync("CanCollapseLabel")).Text.Should().Be("CanCollapse=True");
    }

    [Fact]
    public async Task ExpandingAnimatesToFullContentHeight()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForStableBoundsAsync("ExpanderBox");

        await App.TapAsync("ToggleExpandButton");

        var expanded = await App.WaitForBoundsAsync("ExpanderBox", b => b.Height > 295, TimeSpan.FromSeconds(5));
        expanded.Height.Should().BeApproximately(300, 1.5);

        // Collapse again.
        await App.TapAsync("ToggleExpandButton");

        var collapsed = await App.WaitForBoundsAsync("ExpanderBox", b => b.Height < 105, TimeSpan.FromSeconds(5));
        collapsed.Height.Should().BeApproximately(100, 1.5);
    }

    [Fact]
    public async Task SmallContentDoesNotCollapse()
    {
        await App.OpenTestPageAsync(PageName);

        // Content (40) is smaller than CollapsedHeight (100): nothing to collapse.
        var bounds = await App.WaitForStableBoundsAsync("SmallExpanderBox");
        bounds.Height.Should().BeApproximately(40, 1.5);

        (await App.WaitForElementAsync("SmallCanCollapseLabel")).Text.Should().Be("CanCollapse=False");
    }

    [Fact]
    public async Task GrowingContentBeyondCollapsedHeightEnablesCollapsing()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForStableBoundsAsync("SmallExpanderBox");

        // Grow the content from 40 to 300: the collapsed expander must cap at 100.
        await App.TapAsync("GrowContentButton");

        var bounds = await App.WaitForBoundsAsync("SmallExpanderBox", b => b.Height > 95, TimeSpan.FromSeconds(5));
        bounds.Height.Should().BeApproximately(100, 1.5);

        await App.WaitForTextAsync("SmallCanCollapseLabel", "CanCollapse=True");
    }
}
