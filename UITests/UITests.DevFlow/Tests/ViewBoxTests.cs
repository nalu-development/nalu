using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>ViewBox</c>: content hosting/swapping, <c>ContentBindingContext</c> and <c>IsClippedToBounds</c>.
/// </summary>
public class ViewBoxTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "View Box Tests";

    [Fact]
    public async Task ContentIsDisplayed()
    {
        await App.OpenTestPageAsync(PageName);

        var content = await App.WaitForElementAsync("ViewBoxContentA");
        content.IsVisible.Should().BeTrue();
        content.Text.Should().Be("Content A");
    }

    [Fact]
    public async Task ContentCanBeSwappedAtRuntime()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("ViewBoxContentA");

        await App.TapAsync("SwapContentButton");

        var newContent = await App.WaitForElementAsync("ViewBoxContentB");
        newContent.Text.Should().Be("Content B");
        (await App.FindElementAsync("ViewBoxContentA")).Should().BeNull("the old content must be removed from the visual tree");
    }

    [Fact]
    public async Task ContentBindingContextFlowsToContent()
    {
        await App.OpenTestPageAsync(PageName);

        var animalLabel = await App.WaitForElementAsync("AnimalNameLabel");
        animalLabel.Text.Should().Be("Dog");

        await App.TapAsync("SwitchAnimalButton");

        await App.WaitForTextAsync("AnimalNameLabel", "Cat");
    }

    [Fact]
    public async Task ContentIsClippedToBounds()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("ClipContent");

        // The ViewBox is 100x100 while its content is 200x200: the content overflow
        // at host-relative (150,150) must be clipped away (white host background).
        var clipBox = await App.GetBoundsAsync("ClipBox");
        var content = await App.GetBoundsAsync("ClipContent");

        clipBox.Width.Should().BeApproximately(100, 1.5);
        clipBox.Height.Should().BeApproximately(100, 1.5);
        content.Width.Should().BeApproximately(200, 1.5);
        content.Height.Should().BeApproximately(200, 1.5);

        // Sample inside the host, outside the 100x100 clip area.
        var clippedPixel = await App.GetPixelColorAsync("ClipHost", 150, 150);
        clippedPixel.R.Should().BeGreaterThan(250, "the red overflow must be clipped away (white host background)");
        clippedPixel.G.Should().BeGreaterThan(250);
        clippedPixel.B.Should().BeGreaterThan(250);

        // Disable clipping: the red content must now paint over the host background.
        await App.TapAsync("ToggleClipButton");

        await App.WaitForPixelColorAsync(
            "ClipHost",
            150,
            150,
            static c => c is { R: > 200, G: < 80, B: < 80 }
        );
    }
}
