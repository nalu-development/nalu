namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PopupModels;

public partial class ThreePageModel(INavigationService navigationService, IPopupService popupService) : ObservableObject, ILeavingGuard
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushFourAsync() => navigationService.GoToAsync(Navigation.Relative().Push<FourPageModel>());

    public async ValueTask<bool> CanLeaveAsync()
        // this will leak: https://github.com/CommunityToolkit/Maui/issues/1676
        => (bool)(await popupService.ShowPopupAsync<CanLeavePopupModel>() ?? false);
}
