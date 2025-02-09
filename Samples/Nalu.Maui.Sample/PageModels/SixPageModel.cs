using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class SixPageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task GoToOneAsync() => navigationService.GoToAsync(Navigation.Absolute().ShellContent<OnePageModel>());
}
