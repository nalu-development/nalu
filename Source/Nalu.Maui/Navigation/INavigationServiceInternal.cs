namespace Nalu;

using System.ComponentModel;

internal interface INavigationServiceInternal : INavigationService
{
    Task InitializeAsync<TPageModel>(IShellNavigationController controller, object? intent = null)
        where TPageModel : INotifyPropertyChanged;

    Page CreatePage(Type pageModelType);
}
