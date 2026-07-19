using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>TemplateBox</c>: <c>ContentTemplate</c>, <c>DataTemplateSelector</c>,
/// <c>ContentBindingContext</c> and <c>TemplateContentPresenter</c> projection.
/// </summary>
public class TemplateBoxTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Template Box Tests";

    [Fact]
    public async Task TemplateRendersWithContentBindingContext()
    {
        await App.OpenTestPageAsync(PageName);

        var label = await App.WaitForElementAsync("TemplateALabel");
        label.Text.Should().Be("Alice");
    }

    [Fact]
    public async Task ChangingContentBindingContextUpdatesContent()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("TemplateALabel");

        await App.TapAsync("SwitchPersonButton");

        await App.WaitForTextAsync("TemplateALabel", "Bob");
    }

    [Fact]
    public async Task SwappingTemplateReplacesContent()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("TemplateALabel");

        await App.TapAsync("SwapTemplateButton");

        var newLabel = await App.WaitForElementAsync("TemplateBLabel");
        newLabel.Text.Should().Be("B:Alice");
        (await App.FindElementAsync("TemplateALabel")).Should().BeNull("the old template content must be removed");
    }

    [Fact]
    public async Task TemplateSelectorPicksTemplateByItem()
    {
        await App.OpenTestPageAsync(PageName);

        var evenLabel = await App.WaitForElementAsync("EvenTemplateLabel");
        evenLabel.Text.Should().Be("Even item");

        await App.TapAsync("SwitchSelectorModelButton");

        var oddLabel = await App.WaitForElementAsync("OddTemplateLabel");
        oddLabel.Text.Should().Be("Odd item");
        (await App.FindElementAsync("EvenTemplateLabel")).Should().BeNull("the selector must have replaced the even template");
    }

    [Fact]
    public async Task TemplateContentIsProjectedThroughPresenter()
    {
        await App.OpenTestPageAsync(PageName);

        var prefix = await App.WaitForElementAsync("ProjectionPrefixLabel");
        prefix.Text.Should().Be("Projected => ");

        var projected = await App.WaitForElementAsync("ProjectedLabel");
        projected.Text.Should().Be("I'm here!");
        projected.IsVisible.Should().BeTrue();

        // The projected content must actually be rendered next to the prefix (same row).
        var prefixBounds = await App.GetBoundsAsync("ProjectionPrefixLabel");
        var projectedBounds = await App.GetBoundsAsync("ProjectedLabel");
        projectedBounds.X.Should().BeGreaterThanOrEqualTo(prefixBounds.Right - 1.5);
    }
}
