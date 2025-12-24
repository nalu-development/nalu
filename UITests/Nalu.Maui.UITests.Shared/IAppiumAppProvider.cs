namespace Nalu.Maui.UITests;

public interface IAppiumAppProvider
{
    Task<IAppiumApp> GetAsync();
}
