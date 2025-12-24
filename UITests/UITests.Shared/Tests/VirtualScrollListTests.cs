using FluentAssertions;
using Plugin.Maui.UITestHelpers.Appium;
using Xunit;

namespace UITests;

// This is an example of tests that do not need anything platform specific.
// Typically you will want all your tests to be in the shared project so they are ran across all platforms.
public class VirtualScrollListTests : BaseTest
{
    protected override string? TestPageName => "Virtual Scroll List Tests";

    [Fact]
	public void HeaderIsVisible()
    {
        var label = App.FindElement("HeaderLabel");
        label.IsDisplayed().Should().BeTrue();
    }
    
    [Fact]
    public void FooterIsVisible()
    {
        var label = App.FindElement("FooterLabel");
        label.IsDisplayed().Should().BeTrue();
    }
}
