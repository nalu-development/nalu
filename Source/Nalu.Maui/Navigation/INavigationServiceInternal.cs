namespace Nalu;

using System.ComponentModel;

internal interface INavigationServiceInternal : INavigationService
{
    Task InitializeAsync<TPageModel>(INavigationController controller, object? intent = null)
        where TPageModel : INotifyPropertyChanged;

    Page CreatePage(Type pageModelType);
}
