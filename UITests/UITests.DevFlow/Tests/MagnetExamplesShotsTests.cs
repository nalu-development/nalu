using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Regenerates the docs screenshots for conceptual_docs/layouts-magnet-examples.md from the
/// "Magnet Examples" TestApp page. Skipped unless <c>DOCS_SHOTS_DIR</c> is set:
/// <code>DOCS_SHOTS_DIR=$(pwd)/conceptual_docs/assets/images/magnet dotnet test UITests/UITests.DevFlow --filter MagnetExamplesShots</code>
/// </summary>
public class MagnetExamplesShotsTests(NaluApp app) : BaseUiTest(app)
{
    [Fact]
    public async Task CaptureDocsExampleScreenshots()
    {
        var directory = Environment.GetEnvironmentVariable("DOCS_SHOTS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(directory), "Set DOCS_SHOTS_DIR to regenerate the docs screenshots.");

        Directory.CreateDirectory(directory!);
        await App.OpenTestPageAsync("Magnet Examples");

        for (var i = 1; i <= 10; i++)
        {
            var id = $"Example{i:00}";
            await App.FillAsync("ExampleSelector", i.ToString());
            await App.TapAsync("ShowExampleButton");
            await App.WaitForStableBoundsAsync(id);
            var png = await App.ScreenshotElementAsync(id);
            await File.WriteAllBytesAsync(Path.Combine(directory!, $"magnet-example-{i:00}.png"), png);
        }
    }
}
