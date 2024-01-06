namespace Nalu;

using System.ComponentModel;

internal interface INavigationServiceInternal : INavigationService
{
    void Initialize<TPageModel>(INavigationController controller, object? intent = null)
        where TPageModel : INotifyPropertyChanged;

    Page CreatePage(Type pageModelType);
}
