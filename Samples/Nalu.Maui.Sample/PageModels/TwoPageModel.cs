namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class TwoPageModel(INavigationService navigationService) : ObservableObject, IAppearingAware
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushSixAsync() => navigationService.GoToAsync(Navigation.Relative().Push<SixPageModel>());

    // Simulate long loading times on the page
    public async ValueTask OnAppearingAsync() => await Task.Delay(500).ConfigureAwait(true);
}
