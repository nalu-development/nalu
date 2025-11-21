using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.Sample.PopupModels;

namespace Nalu.Maui.Sample.PageModels;

public partial class ThreePageModel(INavigationService navigationService, IPopupService popupService) : ObservableObject, ILeavingGuard
{
    private static int _instanceCount;
    private Shell AppShell => Shell.Current;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushFourAsync() => navigationService.GoToAsync(Navigation.Relative().Push<FourPageModel>());

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task ReplaceSixAsync() => navigationService.GoToAsync(Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop().Push<SixPageModel>());

    public async ValueTask<bool> CanLeaveAsync()
        => (await popupService.ShowPopupAsync<CanLeavePopupModel, bool>(AppShell)).Result;
}
