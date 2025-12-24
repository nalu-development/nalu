using OpenQA.Selenium.Appium;

namespace Nalu.Maui.UITests;

public interface IAppiumApp
{
    string DeviceName { get; }
    TestDevice TestDevice { get; }
    AppiumDriver Driver { get; }
    
#nullable disable
    IUIElement FindElement(string id) => AppiumQuery.ById(id).FindElements(this).FirstOrDefault();
#nullable enable

    IUIElement FindElement(IQuery query)
    {
        if (query is AppiumQuery appiumQuery)
        {
            return appiumQuery.FindElement(this);
        }

        var queryString = query.ToString() ?? throw new InvalidOperationException($"{nameof(FindElement)} could not get query string");
        var q = new AppiumQuery(queryString);
        return q.FindElement(this);
    }

#nullable disable
    IUIElement FindElementByText(string text) =>
        // Android (text), iOS (label), Windows (Name)
        AppiumQuery.ByXPath("//*[@text='" + text + "' or @label='" + text + "' or @Name='" + text + "']").FindElement(this);
#nullable enable

    IReadOnlyCollection<IUIElement> FindElements(string id) => AppiumQuery.ById(id).FindElements(this).ToList();

    IReadOnlyCollection<IUIElement> FindElementsByText(string text) =>
        // Android (text), iOS (label), Windows (Name)
        AppiumQuery.ByXPath("//*[@text='" + text + "' or @label='" + text + "' or @Name='" + text + "']").FindElements(this);

    IReadOnlyCollection<IUIElement> FindElements(IQuery query)
    {
        if (query is AppiumQuery appiumQuery)
        {
            return appiumQuery.FindElements(this);
        }

        var queryString = query.ToString() ?? throw new InvalidOperationException($"{nameof(FindElement)} could not get query string");
        var q = new AppiumQuery(queryString);
        return q.FindElements(this);
    }
}
