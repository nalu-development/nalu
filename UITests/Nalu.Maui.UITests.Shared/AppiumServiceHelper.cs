using OpenQA.Selenium.Appium.Service;

namespace Nalu.Maui.UITests;

public sealed class AppiumServiceHelper : IDisposable
{
    private const string _defaultHostAddress = "127.0.0.1";
    private const int _defaultHostPort = 4723;

    private readonly AppiumLocalService _appiumLocalService;

    public AppiumServiceHelper(string host = _defaultHostAddress, int port = _defaultHostPort)
    {
        var builder = new AppiumServiceBuilder()
                      .WithIPAddress(host)
                      .UsingPort(port);

        _appiumLocalService = builder.Build();
    }

    public void StartAppiumLocalServer()
    {
        if (_appiumLocalService.IsRunning)
        {
            return;
        }
		
        _appiumLocalService.Start();
    }

    public void Dispose() => _appiumLocalService.Dispose();
}
