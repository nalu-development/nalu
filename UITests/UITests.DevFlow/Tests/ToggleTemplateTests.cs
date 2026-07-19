using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>ToggleTemplate</c>: template switching driven by <c>Value</c>
/// and the single-template (<c>WhenTrue</c> only) scenario.
/// </summary>
public class ToggleTemplateTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Toggle Template Tests";

    [Fact]
    public async Task FalseValueShowsWhenFalseTemplate()
    {
        await App.OpenTestPageAsync(PageName);

        var falseLabel = await App.WaitForElementAsync("WhenFalseLabel");
        falseLabel.Text.Should().Be("FALSE");
        (await App.FindElementAsync("WhenTrueLabel")).Should().BeNull();
    }

    [Fact]
    public async Task TogglingValueSwitchesTemplates()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("WhenFalseLabel");

        await App.TapAsync("ToggleValueButton");

        var trueLabel = await App.WaitForElementAsync("WhenTrueLabel");
        trueLabel.Text.Should().Be("TRUE");
        (await App.FindElementAsync("WhenFalseLabel")).Should().BeNull();

        await App.TapAsync("ToggleValueButton");

        await App.WaitForElementAsync("WhenFalseLabel");
        (await App.FindElementAsync("WhenTrueLabel")).Should().BeNull();
    }

    [Fact]
    public async Task MissingTemplateProducesNoContent()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("ToggleOnlyTrueButton");

        // Value=false with no WhenFalse template: no content must be created at all.
        (await App.WaitForElementOrDefaultAsync("OnlyTrueLabel", TimeSpan.FromSeconds(1))).Should().BeNull();

        await App.TapAsync("ToggleOnlyTrueButton");
        (await App.WaitForElementAsync("OnlyTrueLabel")).Text.Should().Be("EXPENSIVE");

        await App.TapAsync("ToggleOnlyTrueButton");
        await App.WaitForElementGoneAsync("OnlyTrueLabel");
    }
}
