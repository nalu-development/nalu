using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;
using Xunit;

namespace UITests;

// Add a CollectionDefinition together with a ICollectionFixture
// to ensure that the setup only runs once
// xUnit does not have a built-in concept of a fixture that only runs once for the whole test set.
[CollectionDefinition("UITests")]
// ReSharper disable once InconsistentNaming
public sealed class UITestsCollectionDefinition : ICollectionFixture<AppiumSetup>; 

// Add all tests to the same collection as above so that the Appium server is only setup once
[Collection("UITests")]
public abstract class BaseTest
{
	protected IApp App => AppiumSetup.App;
    
    protected virtual string? TestPageName => null;

    public BaseTest()
    {
        if (TestPageName is { } pageName)
        {
            App.WaitForElement("TestName");
            var testNameEntry = App.FindElement("TestName");
            testNameEntry.SendKeys(pageName);
            var loadTestButton = App.FindElement("RunTestButton");
            loadTestButton.Click();
        }
    }
}
