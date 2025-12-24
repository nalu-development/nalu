using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace Nalu.Maui.UITests;

public interface IQuery
{
    IQuery ById(string id);
    IQuery ByName(string name);
    IQuery ByClass(string className);
    IQuery ByAccessibilityId(string id);
    IQuery ByXPath(string xpath);
}

public class AppiumQuery : IQuery
	{
        private const string _classToken = "class";
        private const string _idToken = "id";
        private const string _nameToken = "name";
        private const string _accessibilityToken = "accessibilityid";
        private const string _xPathToken = "xpath";
        private const string _querySeparatorToken = "&";
        private const string _idQuery = _idToken + "={0}";
        private const string _nameQuery = _nameToken + "={0}";
        private const string _accessibilityQuery = _accessibilityToken + "={0}";
        private const string _classQuery = _classToken + "={0}";
        private const string _xPathQuery = _xPathToken + "={0}";
        private readonly string _queryStr;

		public AppiumQuery(string queryStr)
		{
			_queryStr = queryStr;
		}

		public AppiumQuery(AppiumQuery query, string queryStr)
		{
			_queryStr = string.Join(query._queryStr, _querySeparatorToken, queryStr);
		}

		IQuery IQuery.ByClass(string classQuery) => new AppiumQuery(this, string.Format(_classQuery, classQuery));

        IQuery IQuery.ById(string id) => new AppiumQuery(this, string.Format(_idQuery, id));

        IQuery IQuery.ByAccessibilityId(string id) => new AppiumQuery(this, string.Format(_accessibilityQuery, id));

        IQuery IQuery.ByName(string nameQuery) => new AppiumQuery(this, string.Format(_nameQuery, nameQuery));

        IQuery IQuery.ByXPath(string xpath) => new AppiumQuery(this, string.Format(_xPathQuery, Uri.EscapeDataString(xpath)));

        public static AppiumQuery ById(string id) => new(string.Format(_idQuery, id));

        public static AppiumQuery ByName(string nameQuery) => new(string.Format(_nameQuery, nameQuery));

        public static AppiumQuery ByAccessibilityId(string id) => new(string.Format(_accessibilityQuery, id));

        public static AppiumQuery ByClass(string classQuery) => new(string.Format(_classQuery, classQuery));

        public static AppiumQuery ByXPath(string xpath) => new(string.Format(_xPathQuery, Uri.EscapeDataString(xpath)));

#nullable disable
		public IUIElement FindElement(IAppiumApp appiumApp)
		{
			// e.g. class=button&id=MyButton
			var querySplit = _queryStr.Split(_querySeparatorToken);
			var queryStr = querySplit[0];
			var argSplit = queryStr.Split('=');

			if (argSplit.Length != 2)
			{
				throw new ArgumentException("Invalid Query");
			}

			var queryBy = GetQueryBy(argSplit[0], argSplit[1]);
			var foundElement = appiumApp.Driver.FindElements(queryBy).FirstOrDefault();

			if (foundElement == null)
			{
				return null;
			}

			for (var i = 1; i < querySplit.Length; i++)
			{
				foundElement = FindElement(foundElement, querySplit[i]);
			}

			return new AppiumDriverElement(foundElement, appiumApp);
		}

		public IReadOnlyCollection<IUIElement> FindElements(IAppiumApp appiumApp)
		{
			// e.g. class=button&id=MyButton
			var querySplit = _queryStr.Split(_querySeparatorToken);
			var queryStr = querySplit[0];
			var argSplit = queryStr.Split('=');

			if (argSplit.Length != 2)
			{
				throw new ArgumentException("Invalid Query");
			}

			var queryBy = GetQueryBy(argSplit[0], argSplit[1]);
			var foundElements = appiumApp.Driver.FindElements(queryBy);

			// TODO: What is the expected way to handle multiple queries when multiple elements are returned?
			//for(int i = 1; i < querySplit.Length; i++)
			//{
			//    foundElement = FindElement(foundElement, querySplit[i]);
			//}

			return foundElements.Select(e => new AppiumDriverElement(e, appiumApp)).ToList();
		}
#nullable enable

		public IReadOnlyCollection<IUIElement> FindElements(AppiumElement element, IAppiumApp appiumApp)
		{
			var querySplit = _queryStr.Split(_querySeparatorToken);

			AppiumElement appiumElement = element;
			var queryStr = querySplit[0];
			var argSplit = queryStr.Split('=');
			var queryBy = GetQueryBy(argSplit[0], argSplit[1]);
			var foundElements = element.FindElements(queryBy);
			//for (int i = 0; i < querySplit.Length; i++)
			//{
			//    appiumElement = FindElement(appiumElement, querySplit[i]);
			//}

			return foundElements.Select(e => new AppiumDriverElement((AppiumElement)e, appiumApp)).ToList();
		}

		public IUIElement FindElement(AppiumElement element, IAppiumApp appiumApp)
		{
			var querySplit = _queryStr.Split(_querySeparatorToken);

			AppiumElement appiumElement = element;
			for (var i = 0; i < querySplit.Length; i++)
			{
				appiumElement = FindElement(appiumElement, querySplit[i]);
			}

			return new AppiumDriverElement(appiumElement, appiumApp);
		}

		private static IReadOnlyCollection<AppiumElement> FindElements(AppiumElement element, string query)
		{
			var argSplit = query.Split('=');
			if (argSplit.Length != 2)
			{
				throw new ArgumentException("Invalid Query");
			}

			var queryBy = GetQueryBy(argSplit[0], argSplit[1]);
			return element.FindElements(queryBy).Select(e => (AppiumElement)e).ToList();
		}

		private static AppiumElement FindElement(AppiumElement element, string query)
		{
			var argSplit = query.Split('=');
			if (argSplit.Length != 2)
			{
				throw new ArgumentException("Invalid Query");
			}

			var queryBy = GetQueryBy(argSplit[0], argSplit[1]);
			return element.FindElement(queryBy);
		}

		private static By GetQueryBy(string token, string value) => token.ToLowerInvariant() switch
        {
            _classToken => MobileBy.ClassName(value),
            _nameToken => MobileBy.Name(value),
            _accessibilityToken => MobileBy.AccessibilityId(value),
            _idToken => MobileBy.Id(value),
            _xPathToken => By.XPath(Uri.UnescapeDataString(value)),
            _ => throw new ArgumentException("Unknown query type"),
        };
    }
