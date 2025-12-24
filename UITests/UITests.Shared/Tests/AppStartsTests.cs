using FluentAssertions;
using Plugin.Maui.UITestHelpers.Appium;
using Xunit;

namespace UITests;

// This is an example of tests that do not need anything platform specific.
// Typically you will want all your tests to be in the shared project so they are ran across all platforms.
public class AppStartsTests : BaseTest
{
	[Fact]
	public void AppLaunches()
    {
        var label = App.FindElement("AppTitleLabel");
        label.GetText().Should().Be("Nalu.Maui.TestApp");
    }
}
