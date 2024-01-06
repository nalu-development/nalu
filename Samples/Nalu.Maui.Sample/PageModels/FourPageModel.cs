namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class FourPageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task<bool> PopToOneAsync() => navigationService.GoToAsync(Navigation.Absolute().Add<OnePageModel>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task<bool> NavigateToTwoAsync() => navigationService.GoToAsync(Navigation.Absolute().Add<TwoPageModel>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task<bool> NavigateToFiveAsync() => navigationService.GoToAsync(Navigation.Absolute().Add<FivePageModel>());
}
