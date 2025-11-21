namespace Nalu;

internal interface INavigationServiceProviderInternal : INavigationServiceProvider
{
    void SetParent(INavigationServiceProvider parent);
    void SetContextPage(Page page);
}
