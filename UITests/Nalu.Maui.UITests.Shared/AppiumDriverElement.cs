using OpenQA.Selenium.Appium;

namespace Nalu.Maui.UITests;

public interface IUIElementQueryable
{
    IReadOnlyCollection<IUIElement> ById(string id);
    IReadOnlyCollection<IUIElement> ByName(string name);
    IReadOnlyCollection<IUIElement> ByClass(string className);
    IReadOnlyCollection<IUIElement> ByAccessibilityId(string name);
    IReadOnlyCollection<IUIElement> ByQuery(string query);
}

public interface IUIElement : IUIElementQueryable
{
    // ICommandExecution Command { get; }
}

public class AppiumDriverElement : IUIElement
{
    readonly AppiumElement _element;
    readonly IAppiumApp _appiumApp;

    public AppiumDriverElement(AppiumElement element, IAppiumApp appiumApp)
    {
        _appiumApp = appiumApp;
        _element = element ?? throw new ArgumentNullException(nameof(element));
    }

    // public ICommandExecution Command => _appiumApp.CommandExecutor;

    internal AppiumElement AppiumElement => _element;

    public IReadOnlyCollection<IUIElement> ById(string id) => AppiumQuery.ById(id).FindElements(_element, _appiumApp);

    public IReadOnlyCollection<IUIElement> ByClass(string className) => AppiumQuery.ByClass(className).FindElements(_element, _appiumApp);

    public IReadOnlyCollection<IUIElement> ByName(string name) => AppiumQuery.ByName(name).FindElements(_element, _appiumApp);

    public IReadOnlyCollection<IUIElement> ByAccessibilityId(string id) => AppiumQuery.ByAccessibilityId(id).FindElements(_element, _appiumApp);

    public IReadOnlyCollection<IUIElement> ByQuery(string query) => new AppiumQuery(query).FindElements(_element, _appiumApp);
}
