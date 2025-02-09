using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.Sample.Pages;

namespace Nalu.Maui.Sample.PageModels;

public partial class FourPageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PopToOneAsync() => navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.IgnoreGuards).ShellContent<OnePage>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToTwoAsync() => navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.IgnoreGuards).ShellContent<TwoPageModel>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToFiveAsync()
        => navigationService.GoToAsync(Navigation.Absolute(NavigationBehavior.PopAllPagesOnItemChange | NavigationBehavior.IgnoreGuards).ShellContent<FivePageModel>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToSevenAsync() => navigationService.GoToAsync(Navigation.Absolute().ShellContent<SevenPageModel>());
}
