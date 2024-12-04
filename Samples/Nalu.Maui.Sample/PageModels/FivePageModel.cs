namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class FivePageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    [ObservableProperty] private bool _condition;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand]
    private void Toggle() => Condition = !Condition;

    [RelayCommand]
    private Task GoToThreeAsync() => navigationService.GoToAsync(Navigation.Absolute().ShellContent<OnePageModel>().Add<ThreePageModel>());
}
