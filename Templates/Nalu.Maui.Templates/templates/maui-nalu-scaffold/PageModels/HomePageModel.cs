using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiNaluApp.PageModels;

public partial class HomePageModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CounterLabel))]
    private int _count;

    public string CounterLabel => Count switch
    {
        0 => "Click me",
        1 => "Clicked 1 time",
        _ => $"Clicked {Count} times"
    };

    public HomePageModel(INavigationService navigation)
    {
        _navigation = navigation;
    }

    [RelayCommand]
    private void IncrementCounter() => Count++;

    [RelayCommand]
    private Task OpenDetailAsync() => _navigation.GoToAsync(Nav.Push<DetailPageModel>());
}
