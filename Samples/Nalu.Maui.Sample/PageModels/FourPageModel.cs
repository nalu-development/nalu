namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pages;

public partial class FourPageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PopToOneAsync() => navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.IgnoreGuards).ShellContent<OnePage>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToTwoAsync() => navigationService.GoToAsync(Navigation.Relative());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToFiveAsync() => navigationService.GoToAsync(Navigation.Absolute().ShellContent<FivePageModel>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToSevenAsync() => navigationService.GoToAsync(Navigation.Absolute().ShellContent<SevenPageModel>());
}
