using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class FivePageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    [ObservableProperty]
    private bool _condition;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand]
    private void Toggle() => Condition = !Condition;

    [RelayCommand]
    private Task GoToThreeAsync() => navigationService.GoToAsync(Navigation.Absolute().Root<OnePageModel>().Add<ThreePageModel>());
}
