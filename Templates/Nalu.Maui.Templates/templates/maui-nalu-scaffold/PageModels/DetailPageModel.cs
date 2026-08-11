using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiNaluApp.PageModels;

public partial class DetailPageModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public DetailPageModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoToAsync(Nav.Pop());
}
